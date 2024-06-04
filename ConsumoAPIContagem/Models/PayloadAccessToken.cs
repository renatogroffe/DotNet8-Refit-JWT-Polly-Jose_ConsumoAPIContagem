namespace ConsumoAPIContagem.Models;

public class PayloadAccessToken
{
    public string[]? Unique_name { get; set; }
    public string? Jti { get; set; }
    public int Nbf { get; set; }
    public int Exp { get; set; }
    public int Iat { get; set; }
    public string? Iss { get; set; }
    public string? Aud { get; set; }
}