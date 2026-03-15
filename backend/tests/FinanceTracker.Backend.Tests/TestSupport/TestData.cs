using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;

namespace FinanceTracker.Backend.Tests.TestSupport;

public static class TestData
{
    public static User AddUser(ApplicationDbContext dbContext, string email = "user@example.com")
    {
        var user = new User
        {
            Email = email,
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User"
        };

        dbContext.Users.Add(user);
        return user;
    }

    public static Account AddAccount(ApplicationDbContext dbContext, Guid userId, string name, decimal openingBalance, AccountType type = AccountType.BankAccount)
    {
        var account = new Account
        {
            UserId = userId,
            Name = name,
            Type = type,
            CurrencyCode = "INR",
            OpeningBalance = openingBalance,
            CurrentBalance = openingBalance,
            IsArchived = false
        };

        dbContext.Accounts.Add(account);
        return account;
    }

    public static Category AddCategory(ApplicationDbContext dbContext, Guid userId, string name, CategoryType type)
    {
        var category = new Category
        {
            UserId = userId,
            Name = name,
            Type = type,
            IsSystem = false,
            IsArchived = false
        };

        dbContext.Categories.Add(category);
        return category;
    }
}
