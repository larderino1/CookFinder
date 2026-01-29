using System.Text;
using CookFinder.Bot.Models;

namespace CookFinder.Bot.Services.Recipes;

public static class RecipeFormatter
{
    public static string FormatSummary(Recipe recipe)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"*{Escape(recipe.Title)}*");
        builder.AppendLine(Escape(recipe.Description));
        builder.AppendLine();
        builder.AppendLine("*Ingredients*");

        foreach (var ingredient in recipe.Ingredients)
        {
            var quantity = string.IsNullOrWhiteSpace(ingredient.Quantity) ? "" : $" - {ingredient.Quantity}";
            builder.AppendLine($"• {Escape(ingredient.Name)}{Escape(quantity)}");
        }

        builder.AppendLine();
        builder.AppendLine("*Steps*");
        foreach (var step in recipe.Steps.OrderBy(step => step.Order))
        {
            builder.AppendLine($"{step.Order}. {Escape(step.Instruction)}");
        }

        builder.AppendLine();
        builder.AppendLine($"_Saved from {Escape(recipe.SourceUrl)}_");
        return builder.ToString();
    }

    private static string Escape(string value)
    {
        return value
            .Replace("_", "\\_")
            .Replace("*", "\\*")
            .Replace("[", "\\[")
            .Replace("]", "\\]")
            .Replace("(", "\\(")
            .Replace(")", "\\)")
            .Replace("~", "\\~")
            .Replace("`", "\\`")
            .Replace(">", "\\>")
            .Replace("#", "\\#")
            .Replace("+", "\\+")
            .Replace("-", "\\-")
            .Replace("=", "\\=")
            .Replace("|", "\\|")
            .Replace("{", "\\{")
            .Replace("}", "\\}")
            .Replace(".", "\\.")
            .Replace("!", "\\!");
    }
}
