var staffHash = BCrypt.Net.BCrypt.HashPassword("Admin@1234", workFactor: 11);
Console.WriteLine("STAFF: " + staffHash);

var appHash = BCrypt.Net.BCrypt.HashPassword("Applicant@1234", workFactor: 11);
Console.WriteLine("APPLICANT: " + appHash);
