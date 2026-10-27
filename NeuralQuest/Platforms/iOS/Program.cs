using ObjCRuntime;
using UIKit;

namespace NeuralQuest
{
    public class Program
    {
        // Uygulamanın ana giriş noktasıdır.
        static void Main(string[] args)
        {
            // "AppDelegate" yerine farklı bir uygulama temsilci sınıfı kullanmak isterseniz
            // ilgili sınıfı burada belirtebilirsiniz.
            UIApplication.Main(args, null, typeof(AppDelegate));
        }
    }
}
