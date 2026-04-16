using System;
using BCrypt.Net;

class Program
{
    static void Main()
    {
        string staffPass = "Admin@123";
        string appPass = "N@memail44";
        
        Console.WriteLine($"Staff Hash: {BCrypt.Net.BCrypt.HashPassword(staffPass, 11)}");
        Console.WriteLine($"App Hash: {BCrypt.Net.BCrypt.HashPassword(appPass, 11)}");
    }
}
