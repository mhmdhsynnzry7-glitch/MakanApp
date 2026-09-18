# ماکان‌اپ — MakanApp

این پوشه فعلاً یک پروژه C# از نوع ASP.NET Core Web API با هدف `net9.0` دارد. کد موجود شامل نمونه `WeatherForecast`، تولید سند OpenAPI و Swagger UI در محیط توسعه است. رابط کاربری، قابلیت آموزشی، احراز هویت و اتصال دیتابیس پیاده‌سازی نشده‌اند؛ برای اجرای همین نسخه دیتابیس یا حساب کاربری لازم نیست.

[سند پایه تحلیل و توسعه](docs/DevelopmentBaseline.md) نیازمندی‌ها و پیشنهادهای مراحل آینده را توضیح می‌دهد. سناریوهای پذیرش آن سند، تست اجراشده یا قابلیت فعلی پروژه نیستند. قواعد کار روی پروژه در [AGENTS.md](AGENTS.md) آمده‌اند.

## ساختار موجود

```text
MakanApp.sln
AGENTS.md
README.md
docs/
  DevelopmentBaseline.md
MakanApp/
  MakanApp.csproj
  Program.cs
  Controllers/
    WeatherForecastController.cs
  WeatherForecast.cs
  Properties/
    launchSettings.json
  appsettings.json
  appsettings.Development.json
  MakanApp.http
```

- `MakanApp.sln` تنها پروژه `MakanApp/MakanApp.csproj` را در بر دارد.
- `Program.cs` سرویس‌های Controller و OpenAPI، رابط Swagger UI و مسیر پردازش درخواست را تنظیم می‌کند.
- `WeatherForecastController.cs` درخواست `GET /weatherforecast` را با پنج پیش‌بینی نمونه و تصادفی پاسخ می‌دهد؛ این داده‌ها ذخیره نمی‌شوند.
- وابستگی‌های مستقیم NuGet شامل `Microsoft.AspNetCore.OpenApi` نسخه `9.0.5` و `Swashbuckle.AspNetCore.SwaggerUI` نسخه `10.2.3` هستند.
- `launchSettings.json` پروفایل‌های اجرای محلی و `MakanApp.http` درخواست نمونه را نگه می‌دارند.
- پوشه‌های `.vs/` و `.vscode/` و فایل `MakanApp.csproj.user` تنظیمات محلی ویرایشگر هستند؛ `bin/` و `obj/` خروجی ابزارهای build هستند. این فایل‌ها و خروجی‌های publish در Git ثبت نمی‌شوند.

## دریافت پروژه

```powershell
git clone https://github.com/mhmdhsynnzry7-glitch/MakanApp.git
cd MakanApp
```

## پیش‌نیازهای Visual Studio

- Windows و Visual Studio 2022 با workload به نام **ASP.NET and web development**.
- SDK نسخه .NET 9؛ نصب Runtime به‌تنهایی برای build کافی نیست. هدف `net9.0` در Visual Studio 2022 نسخه `17.12` به بعد پشتیبانی می‌شود. ترکیب متناظر با SDK موجود `9.0.300`، Visual Studio `17.14` است؛ برای انتخاب نسخه سازگار به [جدول رسمی Microsoft](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs) مراجعه کنید.
- دسترسی به منبع NuGet برای بازیابی وابستگی‌ها در اولین build.

برای مشاهده SDKهای نصب‌شده، در Terminal اجرا کنید:

```powershell
dotnet --info
```

در این پروژه `global.json` وجود ندارد. نسخه .NET یا چارچوب هدف را برای حل مشکل محیط، بدون تأیید صاحب پروژه تغییر ندهید.

## اجرا در Visual Studio

1. از **File > Open > Project/Solution** فایل `MakanApp.sln` را باز کنید.
2. در **Solution Explorer** روی پروژه `MakanApp` راست‌کلیک و **Set as Startup Project** را انتخاب کنید.
3. پیکربندی **Debug / Any CPU** و پروفایل اجرای **https** را در نوار اجرا انتخاب کنید. پروفایل‌های `http` و `https` برنامه را با Kestrel اجرا می‌کنند؛ پروفایل `IIS Express` نیز برای اجرای محلی موجود است.
4. از **Build > Build Solution** یا میانبر `Ctrl+Shift+B` استفاده کنید و پایان موفق بازیابی NuGet و build را در پنجره **Output** بررسی کنید.
5. با `F5` برای دیباگ یا `Ctrl+F5` بدون دیباگ برنامه را اجرا کنید. برای HTTPS، در صورت درخواست Visual Studio، گواهی توسعه محلی را مورد اعتماد قرار دهید.
6. مرورگر به‌صورت خودکار روی [Swagger UI](https://localhost:7063/swagger) باز می‌شود؛ در پروفایل‌ها `launchBrowser` برابر `true` و `launchUrl` برابر `swagger` است. [پیش‌بینی نمونه](https://localhost:7063/weatherforecast) و [سند OpenAPI](https://localhost:7063/openapi/v1.json) نیز در دسترس‌اند.
7. پس از بررسی، اجرای دیباگ را با **Stop Debugging** یا اجرای کنسولی را با `Ctrl+C` متوقف کنید.

پروفایل‌های فعلی در `MakanApp/Properties/launchSettings.json`:

| پروفایل | نشانی‌ها | محیط |
| --- | --- | --- |
| `https` | `https://localhost:7063` و `http://localhost:5077` | `Development` |
| `http` | `http://localhost:5077` | `Development` |
| `IIS Express` | `https://localhost:44362` و `http://localhost:3576` | `Development` |

برنامه `UseHttpsRedirection` دارد؛ هنگام اجرای پروفایل `https` از نشانی HTTPS استفاده کنید. پروفایل `http` به‌تنهایی پورت HTTPS تعریف نمی‌کند و ممکن است هشدار تعیین پورت تغییرمسیر را نمایش دهد.

مسیر `/` صفحه‌ای ندارد و پاسخ `404` در آن به‌تنهایی نشانه خرابی اجرا نیست. Swagger UI در `/swagger` سند JSON موجود در `/openapi/v1.json` را نمایش می‌دهد و امکان ارسال درخواست آزمایشی به API را فراهم می‌کند. هر دو مسیر مستندات فقط در محیط `Development` فعال هستند.

برای ارسال درخواست از خود Visual Studio، فایل `MakanApp/MakanApp.http` را باز کنید و **Send Request** را بزنید. نشانی فعلی آن `http://localhost:5077` است؛ برای پروفایل `https` می‌توانید مقدار `MakanApp_HostAddress` را در نسخه محلی خود به `https://localhost:7063` تنظیم کنید.

## فرمان‌های معادل در Terminal

از پوشه‌ای که `MakanApp.sln` در آن قرار دارد اجرا کنید:

```powershell
dotnet build MakanApp.sln --configuration Debug --nologo
dotnet run --project MakanApp/MakanApp.csproj --configuration Debug --no-build --launch-profile https
```

فرمان build بازیابی وابستگی‌ها را نیز انجام می‌دهد. اگر گواهی HTTPS توسعه مورد اعتماد نیست، می‌توانید در محیط توسعه محلی فرمان زیر را اجرا و پیام تأیید سیستم را بررسی کنید:

```powershell
dotnet dev-certs https --trust
```

خطای `NU1301` هنگام دسترسی به `https://api.nuget.org/v3/index.json` مربوط به restore است؛ اتصال شبکه، تنظیمات پراکسی و اعتماد گواهی محیط را بررسی کنید. خاموش‌کردن اعتبارسنجی TLS یا تغییر نسخه .NET راه‌حل این مرحله نیست.

## وضعیت بررسی نسخه فعلی

| بررسی | نتیجه واقعی |
| --- | --- |
| SDK اجراشده | `9.0.300` با هدف پروژه `net9.0` |
| `dotnet build MakanApp.sln --configuration Debug --nologo` | موفق؛ صفر خطا و صفر هشدار |
| نخستین تلاش build در محیط محدود | ناموفق با `NU1301` و خطای SSL اتصال NuGet؛ اجرای مجدد همان فرمان با دسترسی تأییدشده موفق شد |
| پروژه یا کد تست | پروژه تست در Solution و فایل‌های پروژه پیدا نشد؛ تست مبتنی بر پروژه اجرا نشده است |
| بررسی اجرایی HTTP | چهار بررسی موفق: صفحه Swagger، اتصال تنظیمات آن به OpenAPI، سند شامل `GET /WeatherForecast` و پاسخ API با پنج رکورد |
| اجرای تعاملی Visual Studio | بررسی نشده است |
| مخزن Git | شاخه محلی `main` با حفظ commit اولیه به `origin/main` متصل شده است؛ وضعیت همگام‌سازی با `git status` قابل بررسی است |

پس از اضافه‌شدن پروژه تست به Solution در مرحله‌ای مجاز، ابتدا build و سپس تست‌های مرتبط را اجرا کنید. برای تست‌های داخل Solution پس از build موفق می‌توان از این فرمان استفاده کرد:

```powershell
dotnet test MakanApp.sln --configuration Debug --no-build --nologo
```

این فرمان در مرحله آماده‌سازی اجرا نشده است، چون پروژه تستی وجود ندارد. خروج موفق فرمان با صفر تست نیز موفقیت تست محسوب نمی‌شود.

نسخه فعلی شامل Web API نمونه، Swagger UI و مستندات توسعه است؛ قابلیت‌های آموزشی سند پایه هنوز پیاده‌سازی نشده‌اند.
