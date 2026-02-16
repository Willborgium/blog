using System.Threading.Tasks;

namespace Blog.Generator
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            await new BlogBuilder().GenerateSiteAsync(args);
        }
    }
}
