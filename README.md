# BlogApp (The Internet Is Not Dead)

This is a blog app created for my college class "Advanced Progamming & Web Development".

This app is written with ASP.NET (MVC), so C#, HTML and JS.

## Getting Started

To get this setup locally, you need to get the Microsoft SQL Database setup. To do this, follow these steps:

1. Ensure Microsoft SQL Server is installed on your machine 
2. Go to your cmd console, and type in the following:
> sqllocaldb create "BLOG-SERVER"
> sqllocaldb start "BLOG-SERVER"
N.B. The default name for the server is BLOG-SERVER, but if you want to define a different name, you'll need to update the string in appsettings.json
3. Go into the BlogApp project in VisualStudio, open the package manager console and type in the following:
> update-database

This will add the relevant tables to the DB server needed for this web app.

## Dependencies

This application does use a few NuGet packages, these include:

- HTMLAgilityPack 1.12.4
- Microsoft.AspNetCore.StaticFiles 2.3.9
- Microsoft.EntityFrameworkCore 9.0.13
- Microsoft.EntityFrameworkCore.SqlServer 9.0.13
- Microsoft.EntityFrameworkCore.Tools 9.0.13

For the text-box functionality, I used QuillJS

Notes:
https://www.youtube.com/watch?v=5lY1BhPjjDg
https://www.youtube.com/watch?v=IBMECNBRcrU