using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SE_PTTVoiceControl
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Patcher patcher = new Patcher(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
                patcher.PatchAssembly();
                Console.WriteLine("Patched Successfully");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.WriteLine();
            }
            finally
            {
                Console.Write("\nPress any key to continue... ");
                Console.ReadKey();
            }
        }
    }
}
