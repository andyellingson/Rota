# Copilot Instructions

## General Guidelines
- Always add comments for new code, especially for complex logic
- Always make use of best practices for error handling and input validation
- Always remove any unused code, variables, or imports
- Prefer readability over cleverness
- Avoid deeply nested conditionals
- Use early returns where appropriate
- Follow existing naming conventions exactly
- Do not introduce new libraries unless explicitly requested

## Code Style
- Prefer using theme variables (var(--color-bg-card), var(--color-border), var(--color-text-primary)) for component card backgrounds to avoid bright white surfaces in dark mode.

## Project-Specific Rules
- User prefers changes in code to use userId (OwnerId) instead of Username for Reminder ownership and requested removal of Reminder.Username property; update services and endpoints accordingly.
- User prefers UI improvements and professional color/contrast adjustments for dark mode in the Manage page.