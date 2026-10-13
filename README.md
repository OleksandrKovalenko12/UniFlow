# UniFlow

System wspomagający elektroniczny obieg dokumentów na uczelni — projekt realizowany
w ramach pracy inżynierskiej.

> **Status:** projekt w trakcie realizacji. Gotowy jest model domeny; implementacja
> w toku.

## O projekcie

UniFlow umożliwia studentom składanie wniosków w formie elektronicznej, ich weryfikację
przez pracowników dziekanatu oraz zatwierdzanie przez prodziekana zgodnie ze zdefiniowanym
przepływem pracy (workflow). System automatycznie generuje dokumenty PDF i rejestruje
pełną historię zmian statusu każdego wniosku (audit trail).

## Planowane funkcjonalności (MVP)

- **Role i uprawnienia** — Student, Pracownik Dziekanatu, Prodziekan, Administrator.
  Pracownicy mają dostęp wyłącznie do wniosków swojego wydziału.
- **Obsługa wielu wydziałów i kierunków** — student studiujący kilka kierunków składa
  wniosek w ramach wybranego kierunku; wniosek trafia do właściwego dziekanatu.
- **Wnioski** — kreator wniosków dla trzech typów podań:
  - zmiana danych osobowych,
  - zmiana grupy zajęciowej,
  - urlop dziekański.
- **Workflow** — maszyna stanów wniosku (złożony, w weryfikacji, do poprawy, ponownie
  złożony, przekazany do prodziekana, zatwierdzony, odrzucony, wycofany).
- **Dokumenty PDF** — generowanie oficjalnego dokumentu z danych formularza; konwersja
  załączników (dokumenty Office, obrazy) do PDF.
- **Audyt** — oś czasu zmian statusu (kto, kiedy, dlaczego), komentarze wewnętrzne,
  archiwizacja załączników zamiast usuwania.

## Stos technologiczny

| Obszar | Technologia |
|---|---|
| Backend | .NET 10, ASP.NET Core Web API |
| Frontend | Blazor WebAssembly, MudBlazor |
| Baza danych | PostgreSQL + Entity Framework Core |
| Konwersja do PDF | [Gotenberg](https://gotenberg.dev) (Docker) |
| Uwierzytelnianie | ASP.NET Core Identity |

## Architektura (planowana)

```
UniFlow.Domain          — encje, statusy, maszyna stanów (bez zależności zewnętrznych)
UniFlow.Infrastructure  — EF Core, PostgreSQL, klient Gotenberga, przechowywanie plików
UniFlow.Shared          — DTO i interfejsy serwisów współdzielone przez Api i Client
UniFlow.Api             — REST API, uwierzytelnianie i autoryzacja, hostuje Client
UniFlow.Client          — interfejs użytkownika (Blazor WebAssembly)
UniFlow.Tests           — testy jednostkowe i integracyjne
```

## Dokumentacja

- [Model domeny](docs/model-domeny.md) — diagram klas, diagram stanów wniosku, tabela
  przejść, typy podań.

## Uruchomienie

Instrukcja zostanie uzupełniona wraz z pierwszą działającą wersją aplikacji.

## Poza zakresem

- powiadomienia e-mail / SMS,
- integracja z USOS (system korzysta z własnej, niezależnej bazy testowej),
- kwalifikowany podpis elektroniczny.
