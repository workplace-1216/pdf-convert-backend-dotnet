using PdfPortal.Scripts;

Console.WriteLine("===========================================");
Console.WriteLine("  Creating Default Admin User");
Console.WriteLine("===========================================");
Console.WriteLine();

try
{
    await CreateDefaultAdmin.CreateAdminUser();
    Console.WriteLine();
    Console.WriteLine("✓ Success!");
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine($"✗ Error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    return 1;
}

return 0;
