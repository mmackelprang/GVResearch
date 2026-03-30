#!/usr/bin/env bash
# GVResearch Environment Setup
# Run once to add the GV API key to your shell profile.
# Supports bash and zsh.

API_KEY="AIzaSyDTYc1N4xiODyrQYK0Kl6g_y279LjYkrBg"
EXPORT_LINE="export GvResearch__ApiKey=\"${API_KEY}\""

# Detect shell profile
if [ -n "$ZSH_VERSION" ]; then
    PROFILE="$HOME/.zshrc"
elif [ -f "$HOME/.bashrc" ]; then
    PROFILE="$HOME/.bashrc"
else
    PROFILE="$HOME/.profile"
fi

# Add if not already present
if ! grep -q "GvResearch__ApiKey" "$PROFILE" 2>/dev/null; then
    echo "" >> "$PROFILE"
    echo "# GVResearch API key" >> "$PROFILE"
    echo "$EXPORT_LINE" >> "$PROFILE"
    echo "Added GvResearch__ApiKey to $PROFILE"
else
    echo "GvResearch__ApiKey already set in $PROFILE"
fi

# Export for current session
eval "$EXPORT_LINE"
echo "GvResearch__ApiKey is now available in this session."
echo "Run 'source $PROFILE' or restart your terminal for persistence."
