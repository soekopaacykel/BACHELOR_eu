using Microsoft.AspNetCore.Identity;
using System;

public static class PasswordHelper
{
    private static PasswordHasher<object> _passwordHasher = new PasswordHasher<object>();

    // Metode til at hashe kodeord
    public static string HashPassword(string password)
    {
        return _passwordHasher.HashPassword(null, password);
    }

    // Metode til at validere kodeord
    public static bool VerifyPassword(string password, string hashedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword))
        {
            throw new ArgumentNullException(nameof(hashedPassword), "Hashed password cannot be null or empty.");
        }

        var result = _passwordHasher.VerifyHashedPassword(null, hashedPassword, password);
        return result == PasswordVerificationResult.Success;
    }

    // Metode til at generere tilfældigt kodeord
    public static string GenerateRandomPassword(int length = 12)
    {
        const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*()";
        char[] password = new char[length];
        
        for (int i = 0; i < length; i++)
        {
            password[i] = validChars[new Random().Next(validChars.Length)];
        }

        return new string(password);
    }
}
