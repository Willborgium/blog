using System.Threading.Tasks;

namespace Blog.Generator
{
    public class Program
    {
        public static async Task Main()
        {
            await new BlogBuilder().GenerateArticlesAsync();
        }
    }
}
