using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Agendamentos.Api.DTOs;

namespace Agendamentos.Api.Services;

// Cliente HTTP do serviço AnimaisTutores (única dependência externa deste serviço).
public interface IAnimaisTutoresClient
{
    Task<Resultado<AnimalExternoDto>> ObterAnimalAsync(int id, CancellationToken ct = default);
}

public class AnimaisTutoresClient(HttpClient http) : IAnimaisTutoresClient
{
    public async Task<Resultado<AnimalExternoDto>> ObterAnimalAsync(int id, CancellationToken ct = default)
    {
        try
        {
            using var resposta = await http.GetAsync($"api/animais/{id}", ct);

            if (resposta.StatusCode == HttpStatusCode.NotFound)
                return Resultado<AnimalExternoDto>.NaoEncontrado($"Animal {id} não encontrado.");

            if (!resposta.IsSuccessStatusCode)
                return Resultado<AnimalExternoDto>.Indisponivel(
                    "O serviço de animais respondeu com erro. Tente novamente em instantes.");

            var animal = await resposta.Content.ReadFromJsonAsync<AnimalExternoDto>(ct);

            return animal is null
                ? Resultado<AnimalExternoDto>.Indisponivel("Resposta inválida do serviço de animais.")
                : Resultado<AnimalExternoDto>.Ok(animal);
        }
        catch (HttpRequestException)
        {
            return Resultado<AnimalExternoDto>.Indisponivel("Não foi possível contatar o serviço de animais.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timeout do HttpClient (cancelamento do próprio cliente HTTP não é cancelamento da requisição)
            return Resultado<AnimalExternoDto>.Indisponivel("O serviço de animais demorou demais para responder.");
        }
        catch (JsonException)
        {
            return Resultado<AnimalExternoDto>.Indisponivel("Resposta inválida do serviço de animais.");
        }
    }
}
