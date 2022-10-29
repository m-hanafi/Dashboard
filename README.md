
<!-- TABLE OF CONTENTS -->
<details>
  <summary>Table of Contents</summary>
  <ol>
    <li>
      <a href="#about-the-project">About The Project</a>
      <ul>
        <li><a href="#built-with">Built With</a></li>
      </ul>
    </li>
    <li><a href="#usage">Installation</a></li>
    <li><a href="#usage">Usage</a></li>
    <li><a href="#acknowledgments">Acknowledgments</a></li>
  </ol>
</details>



<!-- ABOUT THE PROJECT -->
## About The Project

### DASHBOARD

![alt text](https://github.com/m-hanafi/Dashboard/blob/main/Images/dashboard_1.png?raw=true)

The purpose of the dashboard is to visualize real-time garment production data on a browser which will refresh in pre-configured interval. It runs by a windows service and can be accessed on any browser in local network. </br>
The production data is stored into a database by another system and the dashboard visualize the data in charts. </br>
When user enter the valid URL which only can be accessed via local network, the dashboard page will call the API HTTP post method and execute a method function to run  SQL queries to get the data from MS SQL Server database and visualize the data using Highcharts pluggin.  

Visualized data included:
* Total piece loaded.
* Total piece production.
* Total QC count.
* Station piece count.
* Total production by hour.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- DEVELOPMENT TOOLS -->
### Built With

IDE:
* .Net Framework
* Visual Studio

Languages:
* C#
* HTML
* CSS
* Javascript

Pluggin:
* <a href="https://www.highcharts.com/">Highcharts </a>

Database:
* MS SQL Server

<!-- INSTALLATION -->
### Installation

Install the windows service by running the installation file.

<!-- USAGE -->
## Usage

User can access the dashboard from a browser and enter the dashboard local URL.


<!-- ACKNOWLEDGMENTS -->
## Acknowledgments

* [Highcharts](https://www.highcharts.com/)



