#!/usr/bin/env dotnet-script
#r "nuget: BCrypt.Net-Next, 4.0.3"

var hash = BCrypt.Net.BCrypt.HashPassword("Admin@1234", workFactor: 11);
Console.WriteLine(hash);
