using PrinterHub.Core;
using PrinterHub.Languages.Generic;
using PrinterHub.Languages.Godex;
using PrinterHub.Languages.Sato;
using PrinterHub.Languages.Zebra;

namespace PrinterHub.Languages;

public static class LanguageRegistration
{
    /// <summary>Đăng ký tất cả ngôn ngữ lệnh có sẵn.</summary>
    public static LanguageRegistry AddBuiltInLanguages(this LanguageRegistry registry) => registry
        .Register("SBPL", o => new SbplLanguage(o))
        .Register("ZPL", o => new ZplLanguage(o))
        .Register("SZPL", o => new ZplLanguage(o))   // SATO emulation Zebra
        .Register("GZPL", o => new ZplLanguage(o))   // Godex emulation Zebra
        .Register("EZPL", o => new EzplLanguage(o))
        .Register("PJL", o => new PjlLanguage(o))
        .Register("RAW", o => new RawLanguage(o));
}
