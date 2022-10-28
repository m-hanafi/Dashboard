using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Timers;
using System.Web;
using System.Windows.Forms;
using Rockey4NDControl;

namespace Dashboard
{
    public partial class Service1 : ServiceBase
    {
        public Service1()
        {
            InitializeComponent();
        }

        static DateTime TRIAL;
        static string IPAddress;
        SqlConnection con;
        static String LINES = "0";
        static String CLIENT = "0";
        static String SERVER = "0";
        static String TABLET = "0";

        System.Timers.Timer dongletimer = new System.Timers.Timer();

        /// <summary>
        /// Rockey4ND command
        /// </summary>
        enum Ry4Cmd : ushort
        {
            RY_FIND = 1,
            RY_FIND_NEXT,
            RY_OPEN,
            RY_CLOSE,
            RY_READ,
            RY_WRITE,
            RY_RANDOM,
            RY_SEED,
            RY_WRITE_USERID,
            RY_READ_USERID,
            RY_SET_MOUDLE,
            RY_CHECK_MOUDLE,
            RY_WRITE_ARITHMETIC,
            RY_CALCULATE1,
            RY_CALCULATE2,
            RY_CALCULATE3,
            RY_DECREASE
        };

        protected override void OnStart(string[] args)
        {
            try
            {
                var serviceName = "Dashboard";    //service name
                var resetAfter = 60000;     //service reset timer
                //cmd command to restart the service if any failure
                Process.Start("cmd.exe", $"/c sc failure \"{serviceName}\" reset= 1 actions= restart/{resetAfter}/restart/{resetAfter}/restart/{resetAfter}");

                //get current date
                TRIAL = Convert.ToDateTime(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Getlocal_Connection();   //open connection
                IPAddress = GetIPAddress();   //get system ip address

                //open api on all ipaddress of the system
                Uri baseAddress = new Uri("http://0.0.0.0:8091/");
                WebServiceHost svcHost = new WebServiceHost(new APICommandManager("Test"), baseAddress);
                ((ServiceBehaviorAttribute)svcHost.Description.Behaviors[typeof(ServiceBehaviorAttribute)]).InstanceContextMode = InstanceContextMode.Single;

                //start api
                WriteToExFile("API Starting at : " + baseAddress.ToString());
                svcHost.Open();

                WriteToExFile("API Started at : " + baseAddress.ToString());
                string param1 = HttpUtility.ParseQueryString(baseAddress.Query).Get("param1");

                Dongle();    //chekc for the dongle

                //check if pms server is enabled for the dongle
                if (SERVER == "0")
                {
                  WriteToExFile("SERVER not Enabled for this Key");
                  this.Stop();
                }

                //start dongle timer
                dongletimer.Elapsed += new ElapsedEventHandler(OnDongle);
                dongletimer.Interval = 600000;
                dongletimer.Enabled = true;

            }
            catch (Exception ex)
            {
                WriteToExFile(ex.ToString());
            }
        }

        protected override void OnStop()
        {
            WriteToExFile("API is Stopped");
        }

        /// <summary>
        /// Open the database connection
        /// </summary>
        public void Getlocal_Connection()
        {
            try
            {
                con = new SqlConnection("Data Source=127.0.0.1,1433;Network Library=DBMSSOCN;Initial Catalog=ONLINE_LOCALDB;User ID=sa;Password=1234;Connection Timeout=5");
                con.Open();
            }
            catch (Exception ex)
            {
                WriteToExFile(ex.ToString());
            }
        }

        /// <summary>
        /// Read the dongle to check for the activation or trial key
        /// </summary>
        public void Dongle()
        {
            try
            {
                byte[] buffer = new byte[100000];
                ushort handle = 0;
                ushort function = 0;
                ushort p1 = 0;
                ushort p2 = 0;
                ushort p3 = 0;
                ushort p4 = 0;
                uint lp1 = 0;
                uint lp2 = 0;
                int iMaxRockey = 0;
                uint[] uiarrRy4ID = new uint[32];
                string strRet;

                //get activation key for the dongle
                String activationkey = "";
                SqlDataAdapter sda = new SqlDataAdapter("SELECT ACTIVATION_KEY from Setup", con);
                DataTable dt = new DataTable();
                sda.Fill(dt);
                sda.Dispose();
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    activationkey = dt.Rows[i][0].ToString();
                }

                String time = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss");

                //get last hanger time
                SqlDataAdapter sdadongle = new SqlDataAdapter("SELECT TOP(1) CONVERT(VARCHAR(10), TIME, 111) +' '+ CONVERT(VARCHAR(10),TIME, 108) from HANGER_HISTORY order by time desc", con);
                DataTable dtdongle = new DataTable();
                sdadongle.Fill(dtdongle);
                sdadongle.Dispose();
                for (int i = 0; i < dtdongle.Rows.Count; i++)
                {
                    if (dtdongle.Rows[i][0].ToString() != "")
                    {
                        time = dtdongle.Rows[i][0].ToString();
                    }
                }

                //check if activation is empty
                if (activationkey == "")
                {
                    WriteToExFile("No Activation Key");
                    this.Stop();
                }

                //decrypt activation key
                String Serial = Decrypt(activationkey, false);
                String HID = Serial.Substring(8, Serial.Length - 8);   //get HID


                Rockey4ND R4nd = new Rockey4ND();
                R4nd.Rockey(function, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);
                ushort ret = 0;

                for (int j = 0; j < 7; j++)
                {
                    //find dongle
                    p1 = 0xb839; p2 = 0x74bb; p3 = 0x8431; p4 = 0x8788;
                    ret = R4nd.Rockey((ushort)Ry4Cmd.RY_FIND, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);
                    if (0 == ret)
                    {
                        try
                        {
                            uiarrRy4ID[iMaxRockey] = lp1;
                            strRet = string.Format("{0:x8}", uiarrRy4ID[iMaxRockey]);

                            //check if trail version
                            if (HID == "00000000")
                            {
                                for (int i = 0; i < 6; i++)
                                {
                                    p1 = 0xb839; p2 = 0x74bb; p3 = 0x8431; p4 = 0x8788;

                                    //open dongle
                                    ret = R4nd.Rockey((ushort)Ry4Cmd.RY_OPEN, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);
                                    if (0 == ret)
                                    {
                                        uiarrRy4ID[iMaxRockey] = lp1;
                                        iMaxRockey++;
                                        break;
                                    }
                                    else
                                    {
                                        Thread.Sleep(2000);
                                        if (i >= 5)
                                        {
                                            WriteToExFile("Dongle Error : " + ret);
                                            this.Stop();
                                        }
                                    }
                                }

                                for (int i = 0; i < 6; i++)
                                {
                                    p1 = 3;
                                    p2 = 3;
                                    buffer[0] = 0;
                                    //read trail date
                                    ret = R4nd.Rockey((ushort)Ry4Cmd.RY_READ, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);
                                    if (0 == ret)
                                    {
                                        uiarrRy4ID[iMaxRockey] = lp1;
                                        iMaxRockey++;

                                        String date = buffer[0].ToString();
                                        String month = buffer[1].ToString();
                                        String year = buffer[2].ToString();

                                        if (buffer[0].ToString().Length == 1)
                                        {
                                            date = "0" + buffer[0];
                                        }

                                        if (buffer[1].ToString().Length == 1)
                                        {
                                            month = "0" + buffer[1];
                                        }

                                        if (buffer[2].ToString().Length == 2)
                                        {
                                            year = "20" + buffer[2];
                                        }

                                        String temp1 = date + "-" + month + "-" + year;
                                        temp1 = temp1 + " 23:59:59";

                                        DateTime date1 = DateTime.ParseExact(time, "yyyy/MM/dd HH:mm:ss", null);
                                        if (date1 < DateTime.Now)
                                        {
                                            date1 = DateTime.ParseExact(DateTime.Now.ToString("yyyy-MM-dd") + " 23:59:59", "yyyy-MM-dd HH:mm:ss", null);
                                        }

                                        //check if trail expired
                                        DateTime date2 = DateTime.ParseExact(temp1, "dd-MM-yyyy HH:mm:ss", null);
                                        if (date1 > date2)
                                        {
                                            WriteToExFile("Trial Version Expired");
                                            this.Stop();
                                        }
                                        break;
                                    }
                                    else
                                    {
                                        Thread.Sleep(10000);
                                        if (i >= 5)
                                        {
                                            WriteToExFile("Dongle Read Error : " + ret);
                                            this.Stop();
                                        }
                                    }
                                }
                            }
                            //check if activatin key is wrong
                            else if (HID != strRet)
                            {
                                WriteToExFile("Wrong Activation Key. " + HID + ":" + strRet);
                                this.Stop();
                            }

                            iMaxRockey++;
                        }
                        catch (Exception ex)
                        {
                            WriteToExFile(DateTime.Now.ToString() + " : " + ex);
                        }
                        break;
                    }
                    else
                    {
                        Thread.Sleep(10000);
                        if (j >= 6)
                        {
                            WriteToExFile("No Dongle : " + ret);
                            this.Stop();
                        }
                    }
                }

                //check if more than one dingle
                ret = R4nd.Rockey((ushort)Ry4Cmd.RY_FIND_NEXT, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);
                if (0 == ret)
                {
                    uiarrRy4ID[iMaxRockey] = lp1;
                    strRet = string.Format("{0:x8}", uiarrRy4ID[iMaxRockey]);

                    if (HID != strRet)
                    {
                        WriteToExFile("More then One Dongle(s)");
                        this.Stop();
                    }
                    iMaxRockey++;
                }

                //open dongle
                for (int i = 0; i < 6; i++)
                {
                    p1 = 0xb839; p2 = 0x74bb; p3 = 0x8431; p4 = 0x8788;
                    ret = R4nd.Rockey((ushort)Ry4Cmd.RY_OPEN, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);
                    if (0 == ret)
                    {
                        uiarrRy4ID[iMaxRockey] = lp1;
                        iMaxRockey++;
                        break;
                    }
                    else
                    {
                        Thread.Sleep(2000);
                        if (i >= 5)
                        {
                            WriteToExFile("Dongle Error : " + ret);
                            this.Stop();
                        }
                    }
                }

                //read 
                for (int i = 0; i < 6; i++)
                {
                    //Number of Lines
                    p1 = 1;
                    p2 = 1;
                    buffer[0] = 0;
                    ret = R4nd.Rockey((ushort)Ry4Cmd.RY_READ, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);
                    if (0 == ret)
                    {
                        uiarrRy4ID[iMaxRockey] = lp1;
                        iMaxRockey++;
                        LINES = buffer[0].ToString();
                        break;
                    }
                    else
                    {
                        Thread.Sleep(2000);
                        if (i >= 5)
                        {
                            WriteToExFile("Dongle Read Error : " + ret);
                            this.Stop();
                        }
                    }
                }

                //Products Enabled
                for (int i = 0; i < 6; i++)
                {
                    p1 = 6;
                    p2 = 1;
                    buffer[0] = 0;
                    ret = R4nd.Rockey((ushort)Ry4Cmd.RY_READ, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);
                    if (0 == ret)
                    {
                        uiarrRy4ID[iMaxRockey] = lp1;
                        iMaxRockey++;
                        CLIENT = buffer[0].ToString();
                        break;
                    }
                    else
                    {
                        Thread.Sleep(2000);
                        if (i >= 5)
                        {
                            WriteToExFile("Dongle Read Error : " + ret);
                            this.Stop();
                        }
                    }
                }

                //Products Enabled
                for (int i = 0; i < 6; i++)
                {
                    p1 = 7;
                    p2 = 1;
                    buffer[0] = 0;
                    ret = R4nd.Rockey((ushort)Ry4Cmd.RY_READ, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);
                    if (0 == ret)
                    {
                        uiarrRy4ID[iMaxRockey] = lp1;
                        iMaxRockey++;

                        if (buffer[0].ToString() == "1")
                        {
                            SERVER = "1";
                        }
                        break;
                    }
                    else
                    {
                        Thread.Sleep(2000);
                        if (i >= 5)
                        {
                            WriteToExFile("Dongle Read Error : " + ret);
                            this.Stop();
                        }
                    }
                }

                //Products Enabled
                for (int i = 0; i < 6; i++)
                {
                    p1 = 8;
                    p2 = 1;
                    buffer[0] = 0;
                    ret = R4nd.Rockey((ushort)Ry4Cmd.RY_READ, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);
                    if (0 == ret)
                    {
                        uiarrRy4ID[iMaxRockey] = lp1;
                        iMaxRockey++;

                        if (buffer[0].ToString() == "1")
                        {
                            TABLET = "1";
                        }
                        break;
                    }
                    else
                    {
                        Thread.Sleep(2000);
                        if (i >= 5)
                        {
                            WriteToExFile("Dongle Read Error : " + ret);
                            this.Stop();
                        }
                    }
                }


                for (int i = 0; i < 6; i++)
                {
                    //TRIAL VERSION
                    p1 = 3;
                    p2 = 3;
                    buffer[0] = 0;
                    ret = R4nd.Rockey((ushort)Ry4Cmd.RY_READ, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);
                    if (0 == ret)
                    {
                        uiarrRy4ID[iMaxRockey] = lp1;
                        iMaxRockey++;

                        String date = buffer[0].ToString();
                        String month = buffer[1].ToString();
                        String year = buffer[2].ToString();

                        if (buffer[0].ToString().Length == 1)
                        {
                            date = "0" + buffer[0];
                        }

                        if (buffer[1].ToString().Length == 1)
                        {
                            month = "0" + buffer[1];
                        }

                        if (buffer[2].ToString().Length == 2)
                        {
                            year = "20" + buffer[2];
                        }

                        String temp1 = date + "-" + month + "-" + year;
                        temp1 = temp1 + " 23:59:59";
                        TRIAL = DateTime.ParseExact(temp1, "dd-MM-yyyy HH:mm:ss", null);
                        break;
                    }
                    else
                    {
                        Thread.Sleep(2000);
                        if (i >= 5)
                        {
                            WriteToExFile("Dongle Read Error : " + ret);
                            this.Stop();
                        }
                    }
                }

                //close dongle
                for (int i = 0; i < 6; i++)
                {
                    ret = R4nd.Rockey((ushort)Ry4Cmd.RY_CLOSE, ref handle, ref lp1, ref lp2, ref p1, ref p2, ref p3, ref p4, buffer);
                    if (0 == ret)
                    {
                        uiarrRy4ID[iMaxRockey] = lp1;
                        iMaxRockey++;
                        break;
                    }
                    else
                    {
                        Thread.Sleep(2000);
                        if (i >= 5)
                        {
                            WriteToExFile("Dongle Close Error : " + ret);
                            this.Stop();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteToExFile(ex.ToString());
            }
        }

        /// <summary>
        /// //check for the dongle
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        private void OnDongle(object source, ElapsedEventArgs e)
        {
            Dongle();    
        }

        /// <summary>
        /// Generate log file
        /// </summary>
        /// <param name="Message"></param>
        public void WriteToExFile(string Message)
        {
            try
            {
                
                string path = Application.StartupPath + "\\DebugLog\\" + DateTime.Now.ToString("yyyyMMdd");

                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                string filepath = path + "\\Logs_" + DateTime.Now.Date.ToString("yyyy-MM-dd") + ".txt";
                if (!File.Exists(filepath))
                {
                    using (StreamWriter sw = File.CreateText(filepath))
                    {
                        sw.WriteLine(DateTime.Now.ToString("dddd, dd MMMM yyyy HH:mm:ss") + " : " + Message);
                    }
                }
                else
                {
                    using (StreamWriter sw = File.AppendText(filepath))
                    {
                        sw.WriteLine(DateTime.Now.ToString("dddd, dd MMMM yyyy HH:mm:ss") + " : " + Message);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteToExFile("Export Logfile is in Use : " + ex.ToString());
            }
        }

        /// <summary>
        /// Resolves a host name or IP address to an IPHostEntry instance.
        /// </summary>
        /// <returns></returns>
        public String GetIPAddress()
        {
            try
            {
                IPHostEntry Host = default(IPHostEntry);
                string Hostname = null;
                Hostname = System.Environment.MachineName;
                Host = Dns.GetHostEntry(Hostname);

                foreach (IPAddress IP in Host.AddressList)
                {
                    if (IP.AddressFamily == AddressFamily.InterNetwork)
                    {
                        IPAddress = Convert.ToString(IP);
                    }
                }

                return IPAddress;
            }
            catch (Exception ex)
            {
                WriteToExFile(ex.ToString());

                return "";
            }
        }

        /// <summary>
        /// //3-des decrypt
        /// </summary>
        /// <param name="cipherString"></param>
        /// <param name="useHashing"></param>
        /// <returns></returns>
        public string Decrypt(string cipherString, bool useHashing)
        {

            try
            {
                byte[] keyArray;
                byte[] toEncryptArray = Convert.FromBase64String(cipherString);
                string key = "WETHEPEOPLEOFINDIAHAVING";

                if (useHashing)
                {
                    MD5CryptoServiceProvider hashmd5 = new MD5CryptoServiceProvider();
                    keyArray = hashmd5.ComputeHash(UTF8Encoding.UTF8.GetBytes(key));
                    hashmd5.Clear();
                }
                else
                {
                    keyArray = UTF8Encoding.UTF8.GetBytes(key);
                }

                TripleDESCryptoServiceProvider tdes = new TripleDESCryptoServiceProvider();
                tdes.Key = keyArray;
                tdes.Mode = CipherMode.ECB;
                tdes.Padding = PaddingMode.PKCS7;

                ICryptoTransform cTransform = tdes.CreateDecryptor();
                byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
                tdes.Clear();

                return UTF8Encoding.UTF8.GetString(resultArray);
            }
            catch (Exception ex)
            {
                WriteToExFile("Wrong Activation Key");
                this.Stop();
                Console.WriteLine(ex);

                return "";
            }
        }
    }

    class APICommandManager : APIInterface
    {
        //file locations
        String IPAddress;
        String DASHBOARD1 = "C:\\Program Files (x86)\\Dashboard\\HTML\\DASHBOARD1.txt";
        String DASHBOARD2 = "C:\\Program Files (x86)\\Dashboard\\HTML\\DASHBOARD2.txt";

        Service1 sc = new Service1();

        String testt;

        public APICommandManager(String test)
        {
            testt = test;
        }

        //get ip address of the system
        public String GetIPAddress()
        {
            try
            {
                IPHostEntry Host = default(IPHostEntry);
                String Hostname = null;
                Hostname = System.Environment.MachineName;
                Host = Dns.GetHostEntry(Hostname);

                foreach (IPAddress IP in Host.AddressList)
                {
                    if (IP.AddressFamily == AddressFamily.InterNetwork)
                    {
                        IPAddress = Convert.ToString(IP);
                    }
                }

                return IPAddress;
            }
            catch (Exception ex)
            {
                WriteToExFile(ex.ToString());

                return "";
            }
        }

        //generate logs
        public void WriteToExFile(string Message)
        {          
            try
            {
                string path = Application.StartupPath + "\\DebugLog\\" + DateTime.Now.ToString("yyyyMMdd");

                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                string filepath = path + "\\Logs_" + DateTime.Now.Date.ToString("yyyy-MM-dd") + ".txt";
                if (!File.Exists(filepath))
                {
                    using (StreamWriter sw = File.CreateText(filepath))
                    {
                        sw.WriteLine(DateTime.Now.ToString("dddd, dd MMMM yyyy HH:mm:ss") + " : " + Message);
                    }
                }
                else
                {
                    using (StreamWriter sw = File.AppendText(filepath))
                    {
                        sw.WriteLine(DateTime.Now.ToString("dddd, dd MMMM yyyy HH:mm:ss") + " : " + Message);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteToExFile("Export Logfile is in Use : " + ex.ToString());
            }
        }


        public Stream HOME()
        {
            String text1 = "";

            try
            {
                //read file
                if (File.Exists(DASHBOARD2))
                {
                    text1 = File.ReadAllText(DASHBOARD2);
                }

                //open connection
                SqlConnection con2 = new SqlConnection("Data Source=127.0.0.1,1433;Network Library=DBMSSOCN;Initial Catalog=ONLINE_LOCALDB;User ID=sa;Password=1234;Connection Timeout=5"); 
                con2.Open();

                String strSql = "select PRODLINE from SETUP";
                //get Prod Line No
                SqlCommand cmd = new SqlCommand(strSql, con2);
                String strLineNo = cmd.ExecuteScalar().ToString();

                DateTime start = Convert.ToDateTime("1970-01-01 00:00:00");
                String[] color = { "#990000", "#669900", "#000099", "#009999", "#999900", "#269900", "#990073", "#997300", "#00994d", "#4d0099", "#990000", "#994d00", "#004d99", "#992600", "#99004d", "#009973", "#990099", "#739900", "#990026", "#007399", "#4d9900", "#730099", "#009900", "#002699", "#009926", "#260099 " };
                String date = DateTime.Now.ToString("yyyy-MM-dd");

                text1 = text1 + "Highcharts.chart('container', {chart: {backgroundColor: '#201F1F'},title:{text: 'Hourly Production Report : Line - " + strLineNo + "',style: {color: '#efefef'}},subtitle:{text: 'Dashboard ',style: {color: '#efefef'}},yAxis:{title: {text: 'Piece Count',style: {color: '#efefef'}}}, legend:{layout: 'horizontal', align: 'center',verticalAlign: 'bottom',itemStyle: {font: '10pt Trebuchet MS, Verdana, sans-serif',color: 'white'},},plotOptions:{line: {dataLabels:{enabled: true,color: 'white',format: '{y} Pcs',inside: false,style: {fontWeight: 'bold'},}},series: { animation: false, label:{connectorAllowed: false }}},series: [";

                int hourlyflag = 0;


                ////get production details
                int totalunload = 0, totalload = 0;

                strSql = "select distinct MONO from HANGERRECORD where HANGERLOGIN >= '" + date + " 00:00:00' and HANGERLOGIN <= '" + date + " 23:59:59' and PRODUCTIONLINE = '" + strLineNo + "' ";

                SqlDataAdapter sda = new SqlDataAdapter(strSql, con2);
                DataTable dt = new DataTable();
                sda.Fill(dt);
                sda.Dispose();

                for (int j = 0; j < dt.Rows.Count; j++)
                {
                    String mo = dt.Rows[j][0].ToString();
                    //String opcode = dt.Rows[j][1].ToString();

                    hourlyflag = 1;
                    int count = 0;
                    int flag = 0;

                    //text1 = text1 + "\n{ name: \"" + mo + "-" + moline + "\", data :[";
                    text1 = text1 + "\n{ name: \"" + mo + "\", data :[";

                    strSql = "select PRODID from ONLINE_GLOBALDB.dbo.BKPRODUCTION where MONO = '" + mo + "'";
                    SqlCommand cmd2 = new SqlCommand(strSql, con2);
                    String strProdId = cmd2.ExecuteScalar() + "";

                    //get loading opcode
                    strSql = "select top 1 OPCODE from STATIONASSIGN where PRODID = '" + strProdId + "' order by SEQNO asc";
                    SqlCommand cmd3 = new SqlCommand(strSql, con2);
                    String strloadOpCode = cmd3.ExecuteScalar() + "";

                    strSql = "SELECT SUM(PPH) as Pc_Count FROM HANGERRECORD " +
"WHERE HANGERLOGIN>= '" + date + " 00:00:00' and HANGERLOGIN<'" + date + " 23:59:59'  and PRODUCTIONLINE = '" + strLineNo + "' " +
"AND MONO = '" + mo + "' and OPCODE = '" + strloadOpCode + "'  ";

                    SqlCommand cmd1 = new SqlCommand(strSql, con2);
                    String temp1 = cmd1.ExecuteScalar() + "";
                    if (temp1 != "")
                    {
                        count = int.Parse(cmd1.ExecuteScalar() + "");
                        totalload += count;
                    }

                    //get unloading opcode
                    strSql = "select top 1 OPCODE from STATIONASSIGN where PRODID = '" + strProdId + "' order by SEQNO desc";
                    SqlCommand cmd4 = new SqlCommand(strSql, con2);
                    String strUnloadOpCode = cmd4.ExecuteScalar() + "";

                    strSql = "SELECT DATEPART(HOUR, HANGERLOGIN) as Hour, SUM(PPH) as Pc_Count FROM HANGERRECORD " +
"WHERE HANGERLOGIN>= '" + date + " 00:00:00' and HANGERLOGIN<'" + date + " 23:59:59'  and PRODUCTIONLINE = '" + strLineNo + "' " +
"AND MONO = '" + mo + "' and OPCODE = '" + strUnloadOpCode + "'  GROUP BY DATEPART(HOUR, HANGERLOGIN)";

                    SqlDataAdapter sda2 = new SqlDataAdapter(strSql, con2);
                    DataTable dt2 = new DataTable();
                    sda2.Fill(dt2);
                    sda2.Dispose();
                    if (dt2.Rows.Count > 0)
                    {
                        for (int i = 0; i < dt2.Rows.Count; i++)
                        {
                            String temp = dt2.Rows[i][1].ToString();
                            if (temp != "")
                            {
                                count = int.Parse(dt2.Rows[i][1].ToString());
                                totalunload += count;
                            }
                            else
                            {
                                count = 0;
                            }

                            DateTime date1 = Convert.ToDateTime(date + " " + dt2.Rows[i][0].ToString() + ":00:00");
                            double milliseconds = (date1 - start).TotalMilliseconds;

                            text1 += "[" + milliseconds + "," + count + "],";
                            flag = 1;
                        }
                    }


                    if (flag == 1)
                    {
                        text1 = text1.Remove(text1.Length - 1, 1);
                    }

                    text1 = text1 + "]},";
                }

                if (hourlyflag == 1)
                {
                    text1 = text1.Remove(text1.Length - 1, 1);
                }

                text1 += "],";
                text1 += "xAxis: {type: \"datetime\",title: {text: 'Hour of the Day'},labels: {formatter: function() {return Highcharts.dateFormat('%l:%M %p ', this.value);}}},";
                text1 += "responsive: {rules:[{condition:{ maxWidth: 500},chartOptions:{legend: {layout: 'horizontal', align: 'center', verticalAlign: 'bottom' }}}]}});";
                text1 += "Highcharts.chart('container1', {chart:{plotBackgroundColor: null,plotBorderWidth: null,plotShadow: false, type: 'pie',backgroundColor: '#201F1F'},title: { text: 'MO Repair/Rework',style: {color: '#efefef'}},tooltip:{pointFormat: '{series.name}: <b>{point.percentage:.1f}% </b><br>Count : {point.y} Pcs'},accessibility:{point:{ valueSuffix: '%'} },plotOptions:{ pie: { allowPointSelect: true, cursor: 'pointer', dataLabels: { enabled: true,color: 'white',align: \"right\",format: '{y} Pcs',inside: false,style: {fontWeight: 'bold'}, format: '<b>{point.name}</b>: {point.percentage:.1f} % <br> Count : {point.y} Pcs' } } }, series:[{animation: false, name: 'Defects', colorByPoint: true, data: [";

                int qcflag = 0;
                int totalqc = 0;


                strSql = "select MONO,SUM(PPB) from QCREPAIRREC " +
"where CURWKDATE >= '" + date + " 00:00:00' and CURWKDATE<'" + date + " 23:59:59' " +
"group by MONO ORDER BY MONO; ";

                SqlDataAdapter sda3 = new SqlDataAdapter(strSql, con2);
                DataTable dt3 = new DataTable();
                sda3.Fill(dt3);
                sda3.Dispose();

                for (int i = 0; i < dt3.Rows.Count; i++)
                {
                    String mo = dt3.Rows[i][0].ToString();
                    int count = 0;

                    String temp2 = dt3.Rows[i][1].ToString();
                    if (temp2 != "")
                    {
                        count = int.Parse(dt3.Rows[i][1].ToString());
                    }
                    else
                    {
                        count = 0;
                    }

                    totalqc += count;
                    text1 += "\n{ name: '" + mo + "', y: " + count + "},\n";

                    qcflag = 1;
                }

                if (qcflag == 1)
                {
                    text1 = text1.Remove(text1.Length - 1, 1);
                }

                text1 += "]}]});";
                text1 += "Highcharts.chart('container2', {chart: {type: 'column',backgroundColor: '#201F1F'}, title: { text: 'Station : Line - " + strLineNo + "',style: {color: '#efefef'}},legend: {itemStyle: {fontSize:'10px',font: '11pt Trebuchet MS, Verdana, sans-serif', color: 'white'}, itemHoverStyle: {color: '#FFF'}, itemHiddenStyle: { color: '#444' } },xAxis: {categories: ['Line : " + strLineNo + "'],crosshair: true},plotOptions: {series: {animation: false},column: {dataLabels: {enabled: true,crop: false,overflow: 'none',color: 'white'} }},credits: { enabled: false}, series: [";

                int wipflag = 0;

                strSql = "select STATIONID, sum(pph) as pc_count from HANGERRECORD " +
"where CURWKDATE>= '" + date + " 00:00:00' and CURWKDATE<'" + date + " 23:59:59' and PRODUCTIONLINE = '" + strLineNo + "' " +
"group by STATIONID order by STATIONID";

                SqlDataAdapter sda4 = new SqlDataAdapter(strSql, con2);
                DataTable dt4 = new DataTable();
                sda4.Fill(dt4);
                sda4.Dispose();

                for (int i = 0; i < dt4.Rows.Count; i++)
                {
                    String strStnId = dt4.Rows[i][0].ToString();
                    String count = dt4.Rows[i][1].ToString();

                    text1 += "\n{ name: 'Station " + strStnId + "', data: [" + count + "]},\n";
                    wipflag = 1;
                }

                if (wipflag == 1)
                {
                    text1 = text1.Remove(text1.Length - 1, 1);
                }

                text1 += "]});";
                text1 += "Highcharts.chart('container4', {chart: {type: 'column',backgroundColor: '#201F1F'}, title: { text: 'Station : Line - " + strLineNo + "',style: {color: '#efefef'}},legend: {itemStyle: {fontSize:'10px',font: '11pt Trebuchet MS, Verdana, sans-serif', color: 'white'}, itemHoverStyle: {color: '#FFF'}, itemHiddenStyle: { color: '#444' } },xAxis: {categories: ['Line : " + strLineNo + "'],crosshair: true},plotOptions: {series: {animation: false},column: {dataLabels: {enabled: true,crop: false,overflow: 'none',color: 'white'} }},credits: { enabled: false}, series: []});";
                text1 += "</script> ";
                text1 = text1.Replace("unload000", totalunload.ToString());
                text1 = text1.Replace("load000", totalload.ToString());
                text1 = text1.Replace("qc000", totalqc.ToString());
                text1 = text1.Replace("strLineNo", strLineNo);
                text1 = text1 + "<label id=\"local\" for=\"Remote\" hidden>" + GetIPAddress() + "</label>";
                text1 += "</body>";
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                WriteToExFile(ex.Message);
            }

            text1 = text1.Replace("255.255.255.255", GetIPAddress());

            byte[] resultBytes = Encoding.UTF8.GetBytes(text1);
            WebOperationContext.Current.OutgoingResponse.ContentType = "text/html";

            return new MemoryStream(resultBytes);
          
        }

        public Stream FetchImage(String imageName)
        {
            try
            {
                //get images from file
                String filePath = @"C:\\Program Files (x86)\\Dashboard\\Loading\\Images\\" + imageName;
                if (File.Exists(filePath))
                {
                    FileStream fs = File.OpenRead(filePath);
                    WebOperationContext.Current.OutgoingRequest.ContentType = "image/png";

                    return fs;
                }
                else
                {
                    byte[] byteArray = Encoding.UTF8.GetBytes(" Requested Image does not exist :(");
                    MemoryStream strm = new MemoryStream(byteArray);

                    return strm;
                }
            }
            catch (Exception ex)
            {
                byte[] byteArray = Encoding.UTF8.GetBytes(" Requested Image does not exist :(" + ex);
                MemoryStream strm = new MemoryStream(byteArray);

                return strm;
            }
        }

        /// <summary>
        /// get reference files from Loading folder
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public Stream RetrieveFile(String file)
        {
            try
            {
                //get files from folder
                String fileName = @"C:\\Program Files (x86)\\Dashboard\\Loading\\" + file;
                if (File.Exists(fileName))
                {
                    //WebOperationContext.Current.OutgoingResponse.ContentType = "application/octet-stream";
                    FileStream fs = File.OpenRead(fileName);
                    if (fileName.Contains(".css"))
                    {
                        WebOperationContext.Current.OutgoingResponse.ContentType = "text/css";
                    }
                    else
                    {
                        WebOperationContext.Current.OutgoingResponse.ContentType = "text/javascript";
                    }
                    return fs;
                }
                else
                {
                    byte[] byteArray = Encoding.UTF8.GetBytes(" Requested Files does not exist :(");
                    MemoryStream strm = new MemoryStream(byteArray);

                    return strm;
                }
            }
            catch (Exception ex)
            {
                WriteToExFile(ex.ToString());

                byte[] byteArray = Encoding.UTF8.GetBytes(" Requested Files does not exist : " + ex);
                MemoryStream strm = new MemoryStream(byteArray);

                return strm;
            }
        }

        //web post method
        public String webPostMethod(String postData, String URL)
        {
            String responseFromServer = "";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL);
            request.Method = "POST";
            request.Timeout = 5000;
            request.Credentials = CredentialCache.DefaultCredentials;

            ((HttpWebRequest)request).UserAgent = "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 7.1; Trident/5.0)";
            request.Accept = "/";
            request.UseDefaultCredentials = true;
            request.Proxy.Credentials = System.Net.CredentialCache.DefaultCredentials;

            byte[] byteArray = Encoding.UTF8.GetBytes(postData);
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = byteArray.Length;

            Stream dataStream = request.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();

            WebResponse response = request.GetResponse();
            dataStream = response.GetResponseStream();

            StreamReader reader = new StreamReader(dataStream);
            responseFromServer = reader.ReadToEnd();

            reader.Close();
            dataStream.Close();
            response.Close();

            return responseFromServer;
        }
    }

    [ServiceContract]

    public interface APIInterface
    {

        [OperationContract]
        [WebGet(ResponseFormat = WebMessageFormat.Json, UriTemplate = "/HOME")]
        System.IO.Stream HOME();

        [OperationContract]
        [WebGet(UriTemplate = "File/{imageName}")]
        System.IO.Stream FetchImage(String imageName);

        [OperationContract]
        [WebGet(UriTemplate = "Files/{file}")]
        System.IO.Stream RetrieveFile(String file);

    }
}
