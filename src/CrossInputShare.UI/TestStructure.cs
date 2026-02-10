using System;
using System.Collections.Generic;

namespace CrossInputShare.UI
{
    /// <summary>
    /// Test file to verify project structure
    /// </summary>
    public class TestStructure
    {
        public static void VerifyStructure()
        {
            var requiredFiles = new List<string>
            {
                "App.xaml",
                "App.xaml.cs", 
                "MainWindow.xaml",
                "MainWindow.xaml.cs",
                "Program.cs",
                "ViewModels/ViewModelBase.cs",
                "ViewModels/MainViewModel.cs",
                "ViewModels/DeviceViewModel.cs",
                "Converters/BooleanToVisibilityConverter.cs",
                "Converters/ConnectionStatusToColorConverter.cs",
                "Converters/InvertBooleanConverter.cs",
                "Converters/InvertBooleanToVisibilityConverter.cs",
                "Converters/ConnectionStatusToStringConverter.cs",
                "Converters/SessionFeaturesToStringConverter.cs",
                "Controls/DeviceCard.xaml",
                "Controls/DeviceCard.xaml.cs",
                "Controls/CreateSessionDialog.xaml",
                "Controls/CreateSessionDialog.xaml.cs",
                "Controls/VerificationDialog.xaml",
                "Controls/VerificationDialog.xaml.cs",
                "Services/SystemTrayService.cs",
                "README.md"
            };

            Console.WriteLine("UI Project Structure Verification");
            Console.WriteLine("=================================");
            
            int found = 0;
            int missing = 0;
            
            foreach (var file in requiredFiles)
            {
                if (System.IO.File.Exists(System.IO.Path.Combine("CrossInputShare.UI", file)))
                {
                    Console.WriteLine($"✓ {file}");
                    found++;
                }
                else
                {
                    Console.WriteLine($"✗ {file} (MISSING)");
                    missing++;
                }
            }
            
            Console.WriteLine("\nSummary:");
            Console.WriteLine($"Found: {found}");
            Console.WriteLine($"Missing: {missing}");
            Console.WriteLine($"Total: {requiredFiles.Count}");
            
            if (missing == 0)
            {
                Console.WriteLine("\n✅ All required files are present!");
            }
            else
            {
                Console.WriteLine($"\n⚠️  {missing} files are missing!");
            }
        }
    }
}