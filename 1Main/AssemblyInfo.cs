using System.Runtime.InteropServices;
using System.Windows;

// COM görünürlüğü - false yaparak COM bileşenlerine görünmez hale getirir
[assembly: ComVisible(false)]

// WPF Tema Bilgisi
// Bu WPF uygulaması için gerekli tema ayarları
[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,     // tema özgü kaynak sözlüklerinin konumu
    ResourceDictionaryLocation.SourceAssembly  // genel kaynak sözlüğünün konumu
)]

// NOT: 
// AssemblyTitle, AssemblyVersion, AssemblyCompany vb. bilgiler
// modern .NET projelerinde .csproj dosyasında tanımlanır.
// Bu dosyada tekrar tanımlamaya gerek yoktur.