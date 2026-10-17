namespace CrowdSim.OpenGL;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            int? seed = null;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--seed" when i + 1 < args.Length && int.TryParse(args[i + 1], out int parsedSeed):
                        seed = parsedSeed;
                        i++;
                        break;
                }
            }

            using CrowdSimApplication application = new(seed);
            return application.Run();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[FATAL] {exception}");
            return 1;
        }
    }
}
