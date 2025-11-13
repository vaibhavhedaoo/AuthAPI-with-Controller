# ASP.NET Core Identity with Custom Controllers

[![.NET](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/) 
[![Build Status](https://img.shields.io/github/actions/workflow/status/<your-username>/<repo-name>/dotnet.yml?branch=main)](https://github.com/<your-username>/<repo-name>/actions)
[![NuGet](https://img.shields.io/nuget/v/<package-name>)](https://www.nuget.org/packages/<package-name>)

This repository demonstrates how to use **ASP.NET Core Identity** with custom controllers. It allows you to debug and add custom logic in your controllers easily.

---

## Features

- **Custom Identity Controllers**  
  Add your own business logic or validations while using the Identity framework.

- **Logging with Serilog**  
  - Logs can be saved to **Console**, **File**, **Database**, or **AWS CloudWatch**.  
  - Switching between log sinks requires **minimal changes** in configuration.

- **Email Sending**  
  - Sends emails (e.g., after user registration).  
  - Just provide valid credentials in the configuration, and it will start working immediately.

---

## Configuration Example

Add this to your `appsettings.json` (replace placeholder values with real credentials):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "YourDatabaseConnectionString"
  },
  "Jwt": {
    "Key": "YourJWTSecretKey",
    "Issuer": "YourIssuer"
  },
  "Email": {
    "From": "your-email@example.com",
    "SmtpHost": "smtp.example.com",
    "SmtpPort": "587",
    "Username": "your-email@example.com",
    "Password": "your-email-password"
  },
  "AWS": {
    "Region": "us-east-1",
    "AccessKey": "YOUR_AWS_ACCESS_KEY",
    "SecretKey": "YOUR_AWS_SECRET_KEY",
    "LogGroup": "AuthAPI-Logs",
    "LogStreamPrefix": "api"
  }
}



<img width="1786" height="977" alt="image" src="https://github.com/user-attachments/assets/0cc5b2e0-fa9d-4a0c-b83a-1856e0ce5ae9" />

<img width="1543" height="647" alt="image" src="https://github.com/user-attachments/assets/069e1304-9308-40c7-b565-6756f08d1081" />

<img width="1479" height="850" alt="image" src="https://github.com/user-attachments/assets/170b1894-1dd1-47b0-afb6-eb1d4de36b53" />



