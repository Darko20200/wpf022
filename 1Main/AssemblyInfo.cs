using System.Runtime.InteropServices;
using System.Windows;

// COM g�r�n�rl��� - false yaparak COM bile�enlerine g�r�nmez hale getirir
[assembly: ComVisible(false)]

// WPF Tema Bilgisi
// Bu WPF uygulamas� i�in gerekli tema ayarlar�
[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,     // tema �zg� kaynak s�zl�klerinin konumu
    ResourceDictionaryLocation.SourceAssembly  // genel kaynak s�zl���n�n konumu
)]

// NOT: 
// AssemblyTitle, AssemblyVersion, AssemblyCompany vb. bilgiler
// modern .NET projelerinde .csproj dosyas�nda tan�mlan�r.
// Bu dosyada tekrar tan�mlamaya gerek yoktur.