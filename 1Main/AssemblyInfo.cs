using System.Runtime.InteropServices;
using System.Windows;

// COM görünürlüðü - false yaparak COM bileþenlerine görünmez hale getirir
[assembly: ComVisible(false)]

// WPF Tema Bilgisi
// Bu WPF uygulamasý için gerekli tema ayarlarý
[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,     // tema özgü kaynak sözlüklerinin konumu
    ResourceDictionaryLocation.SourceAssembly  // genel kaynak sözlüðünün konumu
)]

// NOT: 
// AssemblyTitle, AssemblyVersion, AssemblyCompany vb. bilgiler
// modern .NET projelerinde .csproj dosyasýnda tanýmlanýr.
// Bu dosyada tekrar tanýmlamaya gerek yoktur.