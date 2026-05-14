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

## POE Part 2 Changes
- Implemented keyword recognition for cybersecurity topics such as passwords, phishing, scams, and privacy.
- Added cybersecurity awareness tips and guidance based on user input.
- Introduced randomised chatbot responses to make interactions more engaging and less repetitive.
- Used arrays/lists to store multiple predefined responses for common cybersecurity questions.
- Improved conversation flow to allow follow-up questions without restarting the chat.
- Enhanced the chatbot’s ability to maintain context during conversations.
- Refined menu navigation and overall user interaction experience.
- Improved chatbot response handling for clearer and more natural communication.
- Organised the application using multiple classes and methods for cleaner, more maintainable code.
- Enhanced the overall usability, responsiveness, and functionality of the Cybersecurity Awareness Chatbot.
