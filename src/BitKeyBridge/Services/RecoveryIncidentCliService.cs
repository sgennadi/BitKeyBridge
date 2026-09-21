using System.Text.Json;

namespace BitKeyBridge;

public static class RecoveryIncidentCliService
{
    public static int Verify(
        string sessionId)
    {
        try
        {
            var result =
                new RecoveryIncidentVerificationService()
                    .Verify(sessionId);

            Console.WriteLine(
                JsonSerializer.Serialize(
                    result,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy =
                            JsonNamingPolicy.CamelCase
                    }));

            return result.Status switch
            {
                "Valid" => 0,
                "NotFullyRetained" => 5,
                _ => 4
            };
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
