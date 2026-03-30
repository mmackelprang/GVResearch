# GVResearch Environment Setup
# Run once (elevated not required) to set the GV API key as a persistent user environment variable.
# After running, restart your terminal for the variable to take effect.

$apiKey = "AIzaSyDTYc1N4xiODyrQYK0Kl6g_y279LjYkrBg"

[System.Environment]::SetEnvironmentVariable("GvResearch__ApiKey", $apiKey, [System.EnvironmentVariableTarget]::User)

Write-Host "Set GvResearch__ApiKey for current user." -ForegroundColor Green
Write-Host "Restart your terminal or IDE for the change to take effect."
