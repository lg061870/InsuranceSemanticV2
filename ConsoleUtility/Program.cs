using System.Text.Json;

var json = File.ReadAllText("insurance_rules.json");
var rules = JsonSerializer.Deserialize<List<InsuranceRule>>(json, new JsonSerializerOptions {
    PropertyNameCaseInsensitive = true
});

// access
var first = rules[0];
Console.WriteLine(first.Carrier);
Console.WriteLine(first.Cardiovascular?["ANGINA"]);
Console.WriteLine(first.AdditionalFields?["AIDS__ARC_OR_HIV"]);
