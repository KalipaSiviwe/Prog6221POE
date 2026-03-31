# CyberBuddy (Programming 2A - Part 1)
CyberBuddy is a C# console chatbot that greets users with audio, shows ASCII art, and answers basic cybersecurity questions.
## Features
- Voice greeting (`Assets/Greetings.wav`)
- ASCII title/banner
- Personalized name greeting
- Cybersecurity Q&A (passwords, phishing, safe browsing, malware, privacy)
- Input validation and fallback response
- Colored UI, dividers, and typing effect
- GitHub Actions CI workflow
## Project Structure
- `Program.cs`
- `Models/Chatbot.cs`
- `Services/ResponseService.cs`
- `Services/ConsoleStyler.cs`
- `.github/workflows/dotnet-ci.yml`
- `assets/greeting.wav`
## Run
dotnet restore
dotnet build
dotnet run
