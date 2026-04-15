using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

class Program {
    static async Task Main() {
        var client = new HttpClient();
        var loginStr = ""{\""mobile\"":\""9877777777\"",\""password\"":\""N@memail44\""}"";
        var content = new StringContent(loginStr, Encoding.UTF8, ""application/json"");
        var res = await client.PostAsync(""http://localhost:5246/api/applicant/auth/login"", content);
        var resStr = await res.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(resStr);
        var token = json.RootElement.GetProperty(""token"").GetString();
        
        client.DefaultRequestHeaders.Add(""Authorization"", ""Bearer "" + token);
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(""NEW""), ""ApplicationType"");
        form.Add(new StringContent(""RETAIL""), ""LicenseCategory"");
        form.Add(new StringContent(""MyFirm""), ""FirmName"");
        form.Add(new StringContent(""Mahottari""), ""AddressDistrict"");
        
        var submitRes = await client.PostAsync(""http://localhost:5246/api/applicant/submit"", form);
        var errStr = await submitRes.Content.ReadAsStringAsync();
        Console.WriteLine(((int)submitRes.StatusCode) + "" "" + errStr);
    }
}
