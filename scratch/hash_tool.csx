#!/usr/bin/env dotnet-script
#r "nuget: BCrypt.Net-Next, 4.0.3"

if (Args.Count == 0) {
    Console.WriteLine("Please provide a password to hash.");
    return;
}

var hash = BCrypt.Net.BCrypt.HashPassword(Args[0], workFactor: 11);
Console.WriteLine(hash);
