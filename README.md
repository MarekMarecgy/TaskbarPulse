<p align="center"><img src="assets/TaskbarPulse.png" alt="TaskbarPulse" width="96"></p>

# TaskbarPulse

**Monitor CPU, RAM, GPU, sieci i dysku bezpośrednio na pasku zadań Windows.**

[Pobierz EXE](https://github.com/MarekMarecgy/TaskbarPulse/releases/latest/download/TaskbarPulse.exe) · [Wydania i paczka ZIP](https://github.com/MarekMarecgy/TaskbarPulse/releases/latest)

## Szybki start

1. Pobierz `TaskbarPulse.exe` z sekcji Releases lub rozpakuj paczkę ZIP.
2. Umieść program w docelowym folderze i uruchom EXE. Instalator nie jest potrzebny.
3. Otwórz ustawienia dwuklikiem na panelu. Moduły możesz włączać i wyłączać od razu.
4. Jeśli chcesz uruchamiać widget wraz z Windows, zaznacz autostart w ustawieniach.

Program wymaga Windows x64 i .NET Framework 4.8. Nie dodaje ikony do zasobnika. EXE nie jest podpisany cyfrowo.

## Funkcje

- CPU, RAM, GPU, pobieranie/wysyłanie sieci oraz opcjonalna aktywność dysku.
- Automatyczne przytulanie do ikon zasobnika i szerokość zależna od włączonych modułów.
- Stała szerokość kafelka sieci: odczyty nie powodują skakania panelu.
- Motyw Windows, własne kolory, rozmiar tekstu, częstotliwość odświeżania i wybór karty sieciowej.
- Polska konfiguracja, opcjonalny autostart i ukrywanie podczas pełnego ekranu.

## Opis


Przenośny monitor zasobów dla Windows 10/11 x64. Interfejs po polsku. C# / Windows Forms, systemowy .NET Framework 4.8. Bez pobierania bibliotek, uprawnień administratora i wysyłania danych.

## Uruchomienie

Uruchom TaskbarPulse.exe. Panel pojawi się po prawej stronie głównego, poziomego paska zadań, bezpośrednio przed ikonami zasobnika. Automatycznie śledzi ich krawędź co 200 ms, również po zmianie szerokości zasobnika i ponownym uruchomieniu Explorera. Dwuklik na panelu otwiera ustawienia. Prawy przycisk: ustawienia, Menedżer zadań, zakończenie. Widget działa bez dodatkowej ikony w zasobniku. Ustawienia i zakończenie są dostępne bezpośrednio na panelu. Zamknięcie ustawień nie zamyka widgetu. Działa jedna instancja na sesję.

## Ustawienia

- Włączanie CPU, RAM, GPU, sieci oraz dysku; minimum jeden moduł. Zmiana modułów działa od razu i jest automatycznie zapisywana. Dysk jest domyślnie wyłączony.
- Automatyczne przytulanie do ikon po prawej (domyślnie włączone), odstęp od ikon (domyślnie 6 px) i rozmiar tekstu. Szerokość obliczana jest automatycznie z rozmiaru czcionki i wybranych modułów. Kafelek sieci ma stałą szerokość z rezerwą na długie odczyty, a wartości są wyrównane do prawej; zmiany ruchu nie przesuwają panelu. Działa to również przy włączonym tylko jednym module. Ręczna szerokość z poprzednich wersji jest ignorowana.
- Po wyłączeniu automatycznej pozycji dostępne jest ręczne odsunięcie od lewej krawędzi.
- Odświeżanie od 500 do 5000 ms.
- Wszystkie aktywne karty sieciowe lub konkretna karta. Przy VPN wybierz konkretną kartę, aby uniknąć podwójnego liczenia.
- Kolory zgodne z Windows lub własne tło i tekst, własny akcent.
- Paski obciążenia i ukrywanie przy pełnym ekranie.
- Autostart przy logowaniu, domyślnie wyłączony. Włącz go po umieszczeniu EXE w docelowym folderze; po przeniesieniu programu wyłącz i włącz ponownie.

Przełączniki modułów zapisują się od razu. Naciśnij Zastosuj, aby zapisać pozostałe opcje. Ustawienia: `%LOCALAPPDATA%\TaskbarPulse\settings.xml`. Autostart: wartość TaskbarPulse w `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

## Pomiary i ograniczenia

CPU: wykorzystanie procesorów logicznych, różnice GetSystemTimes. RAM: procent zajętej pamięci fizycznej, szczegóły GB w dymku. GPU: największe obciążenie pojedynczego silnika po zsumowaniu procesów dla tego silnika, spośród wszystkich kart; wymaga obsługi liczników WDDM przez sterownik. Brak danych jest oznaczony kreską. Pierwsza pełna próbka wymaga około dwóch cykli.

DYSK: procent czasu aktywności najbardziej obciążonego dysku fizycznego (100 minus licznik PhysicalDisk % Idle Time, z pominięciem instancji _Total). To obciążenie odczytem/zapisem, nie procent zajętej pojemności. Odczyty są zbierane tylko, gdy moduł jest włączony. Przy niedostępnym liczniku wyświetlana jest kreska; po włączeniu potrzeba dwóch próbek.

Sieć: bajty na sekundę, K = KiB, M = MiB, G = GiB, T = TiB. Obejmuje ruch lokalny interfejsu; nie jest testem prędkości łącza.

Panel ma zaokrąglone rogi, delikatne obramowanie i gradient, oddzielne kolory CPU/RAM/GPU oraz dwie linie ruchu sieciowego. Jest osobnym oknem nad paskiem zadań. Kotwicę stanowi rzeczywista lewa krawędź systemowego obszaru TrayNotifyWnd. Nie rezerwuje miejsca ani nie przesuwa przycisków otwartych aplikacji; przy bardzo zapełnionym pasku można wyłączyć zbędne moduły lub zmniejszyć tekst. Gdy zasobnik chwilowo nie jest dostępny, panel chowa się i wraca po jego odnalezieniu. Obsługiwany jest główny poziomy pasek, dolny lub górny. Pionowy pasek nie jest obsługiwany. Panel chowa się wraz z automatycznie ukrywanym paskiem. Jednolite tło może różnić się od przezroczystego tła paska. Nie wymaga modyfikacji ani restartu Explorera.

## Kompilacja i diagnostyka

Uruchom z folderu projektu w PowerShell:

```powershell
.\build.ps1
```

Możesz wskazać osobne miejsce na wynik, np. podczas pracy z uruchomionym widgetem:

```powershell
.\build.ps1 -OutputPath C:\Temp\TaskbarPulse.exe
```

Wystarcza kompilator C# dostarczany z .NET Framework 4.8; nie jest wymagany .NET SDK ani pakiety NuGet. Wynik domyślny to `TaskbarPulse.exe` w katalogu projektu. Zamknij uruchomiony widget przed nadpisaniem jego EXE.

Źródła znajdują się w `src/`, ikona w `assets/`, generator ikony w `tools/`. Pliki EXE i paczki dystrybucyjne są udostępniane w Releases i nie są śledzone w repozytorium.

`TaskbarPulse.exe --settings` otwiera ustawienia przy uruchomieniu.

`TaskbarPulse.exe --diagnose` zapisuje odczyty w `%LOCALAPPDATA%\TaskbarPulse\diagnostics.txt` i kończy działanie. `--preview` to tryb kontroli wyglądu, w którym panel jest widoczny w przełączniku okien. Przy zwykłym używaniu nie jest potrzebny.

`TaskbarPulse.exe --layout-info` zapisuje wykryte granice paska, zasobnika i aktualnego panelu w `%LOCALAPPDATA%\TaskbarPulse\layout.txt`. Wartość ACTUAL_GAP wskazuje rzeczywisty odstęp w pikselach fizycznych. EXPECTED_96DPI zakłada skalowanie 100%. Szerokość kafelka sieciowego jest niezależna od aktualnego ruchu.

Błędy interfejsu, jeśli wystąpią: `%LOCALAPPDATA%\TaskbarPulse\error.log`.

Aby usunąć: wyłącz autostart, wybierz Zakończ i usuń folder programu. Ustawienia w LocalAppData można usunąć osobno.

Dokumentacja API:
- https://learn.microsoft.com/en-us/windows/win32/api/pdh/nf-pdh-pdhgetformattedcounterarrayw
- https://learn.microsoft.com/en-us/windows/win32/api/pdh/nf-pdh-pdhaddenglishcounterw
- https://www.microsoft.com/en-us/windows/windows-11-specifications

## Ikona aplikacji

Własna ikona jest osadzona w EXE i wyświetlana w oknie ustawień; nie dodaje ikony do zasobnika. Pliki: `assets/TaskbarPulse.ico` (16, 20, 24, 32, 40, 48, 64, 128, 256 px), `assets/TaskbarPulse.png` (podgląd). Źródło wektorowego rysowania: `tools/GenerateIcon.cs`. Skrypt build.ps1 automatycznie osadza plik ICO podczas kompilacji.

## Weryfikacja pobrania

Do wydania dołączony jest plik `SHA256SUMS.txt`. Sumę pobranego pliku sprawdzisz poleceniem:

```powershell
Get-FileHash .\TaskbarPulse.exe -Algorithm SHA256
```

Porównaj ją z wpisem w pliku sum kontrolnych tego samego wydania.
