using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Retry;
using Refit;
using Serilog.Core;
using ConsumoAPIContagem.Extensions;
using ConsumoAPIContagem.Interfaces;
using ConsumoAPIContagem.Models;

namespace ConsumoAPIContagem.Clients;

public class APIContagemClient : IDisposable
{
    private ILoginAPI? _loginAPI;
    private IContagemAPI? _contagemAPI;
    private IConfiguration? _configuration;
    private Logger? _logger;
    private Token? _token;
    private AsyncRetryPolicy? _jwtPolicy;
    private JsonSerializerOptions? _serializerOptions;

    public bool IsAuthenticatedUsingToken
    {
        get => _token?.Authenticated ?? false;
    }

    public APIContagemClient(IConfiguration configuration,
        Logger logger)
    {
        _configuration = configuration;
        _logger = logger;

        string urlBase = _configuration.GetSection(
            "APIContagem_Access:UrlBase").Value!;

        _loginAPI = RestService.For<ILoginAPI>(urlBase);
        _contagemAPI = RestService.For<IContagemAPI>(urlBase);
        _jwtPolicy = CreateAccessTokenPolicy();
        _serializerOptions = new JsonSerializerOptions() { WriteIndented = true };
    }

    public async Task Autenticar()
    {
        try
        {
            // Envio da requisição a fim de autenticar
            // e obter o token de acesso
            _token = await _loginAPI!.PostCredentialsAsync(
                new ()
                {
                    UserID = _configuration!.GetSection("APIContagem_Access:UserID").Value,
                    Password = _configuration.GetSection("APIContagem_Access:Password").Value
                });
            _logger!.Information("Token JWT:" +
                Environment.NewLine +
                FormatJSONPayload<Token>(_token));
            _logger.Information("Payload do Access Token JWT:" +
                Environment.NewLine +
                FormatJSONPayload<PayloadAccessToken>(
                    Jose.JWT.Payload<PayloadAccessToken>(_token.AccessToken)));
        }
        catch
        {
            _token = null;
            _logger!.Error("Falha ao autenticar...");
        }
    }

    private string FormatJSONPayload<T>(T payload) =>
        JsonSerializer.Serialize(payload,
            new JsonSerializerOptions() { WriteIndented = true });

    private AsyncRetryPolicy CreateAccessTokenPolicy()
    {
        return Policy
            .HandleInner<ApiException>(
                ex => ex.StatusCode == HttpStatusCode.Unauthorized)
            .RetryAsync(1, async (ex, retryCount, context) =>
            {
                var corAnterior = Console.ForegroundColor;

                Console.ForegroundColor = ConsoleColor.Red;
                await Console.Out.WriteLineAsync(
                    Environment.NewLine + "Token expirado ou usuário sem permissão!");
                Console.ForegroundColor = corAnterior;

                Console.ForegroundColor = ConsoleColor.Green;
                await Console.Out.WriteLineAsync(
                    Environment.NewLine + "Execução de RetryPolicy..." +
                    Environment.NewLine);
                Console.ForegroundColor = corAnterior;

                await Autenticar();
                if (!(_token?.Authenticated ?? false))
                    throw new InvalidOperationException("Token inválido!");

                context["AccessToken"] = _token.AccessToken;
            });
    }

    public async Task ExibirResultadoContador()
    {
        var retorno = await _jwtPolicy!.ExecuteWithTokenAsync<ResultadoContador>(
            _token!, async (context) =>
        {
            var resultado = await _contagemAPI!.ObterValorAtualAsync(
              $"Bearer {context["AccessToken"]}");
            return resultado;
        });
        _logger!.Information("Retorno da API de Contagem: " +
            Environment.NewLine +
            FormatJSONPayload<ResultadoContador>(retorno));
    }

    public void Dispose()
    {
        _loginAPI = null;
        _contagemAPI = null;
        _configuration = null;
        _logger = null;
        _token = null;
        _jwtPolicy = null;
        _serializerOptions = null;
    }
}