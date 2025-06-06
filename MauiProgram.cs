using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
#if ANDROID
using NfcDemo.Platforms.Android;
#endif
//using NfcDemo.Platforms

namespace NfcDemo;

public static class MauiProgram
{
	internal static IServiceProvider Services;

    public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			})
        .UseMauiCommunityToolkit();
        builder.Services.AddSingleton<INfcService>(provider =>
        {
			#if ANDROID
						return new NfcAndroidService();
			#else
				return new DummyNfcService();
			#endif
        });


#if DEBUG
        builder.Logging.AddDebug();
#endif
        Services = builder.Services.BuildServiceProvider();

        return builder.Build();
	}
}
