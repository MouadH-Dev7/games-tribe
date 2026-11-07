using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HollowKnightArabicLauncher;

public sealed class TextCatalog : INotifyPropertyChanged
{
    private bool _arabic = true;
    public event PropertyChangedEventHandler? PropertyChanged;
    public bool IsArabic
    {
        get => _arabic;
        set
        {
            if (_arabic == value) return;
            _arabic = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsArabic)));
        }
    }
    public string this[string key] => (_arabic ? Arabic : English).TryGetValue(key, out var value) ? value : key;
    private static readonly IReadOnlyDictionary<string, string> Arabic = new Dictionary<string, string>
    {
        ["Title"]="مشغّل تعريب Hollow Knight: Godmaster", ["Subtitle"]="إدارة حزمة التعريب المحلية بأمان",
        ["GamePath"]="مجلد اللعبة", ["PackagePath"]="مجلد حزمة التعريب", ["Browse"]="استعراض…", ["Refresh"]="فحص من جديد",
        ["GameStatus"]="حالة اللعبة", ["PackageStatus"]="حالة الحزمة", ["Install"]="تثبيت التعريب", ["Uninstall"]="إزالة التعريب واستعادة الملفات",
        ["Recover"]="استعادة عملية متوقفة", ["Launch"]="تشغيل اللعبة", ["Language"]="English / العربية", ["Compatibility"]="التوافق",
        ["Log"]="سجل العمليات", ["Ready"]="جاهز للفحص", ["Warning"]="التوافق غير مؤكد؛ ملفات الإضافات غير موقعة.",
        ["NoGame"]="لم يتم التحقق من تثبيت اللعبة.", ["NoPackage"]="لم تجتز الحزمة التحقق.", ["Installed"]="التعريب مثبت وتم التحقق من ملفاته.",
        ["RecoverNone"]="لا توجد عملية متوقفة.", ["Busy"]="جارٍ العمل…", ["Manual"]="توجد ملفات تحتاج إلى مراجعة يدوية.",
        ["InstallConfirmTitle"]="تأكيد تثبيت مكونات التعريب", ["InstallConfirm"]="تتضمن الحزمة ملفات تشغيل ستحمّلها اللعبة عند بدء التشغيل. تابع فقط إذا كنت تثق بمصدر الحزمة.",
        ["UninstallConfirmTitle"]="تأكيد إزالة التعريب", ["UninstallConfirm"]="ستزال فقط الملفات التي أنشأتها هذه العملية، وستستعاد النسخة الأصلية المحفوظة. الملفات التي تغيّرت بعد التثبيت ستُترك دون حذف.",
        ["RecoverConfirmTitle"]="استعادة عملية غير مكتملة", ["RecoverConfirm"]="سيتم فحص سجل العملية والتراجع فقط عن التغييرات التي تطابق بصمات هذه العملية. متابعة؟",
        ["LaunchConfirmTitle"]="تشغيل مع توافق غير مؤكد", ["LaunchConfirm"]="لم يتم اختبار التوافق الفعلي. ستُحمّل اللعبة ملفات الإضافات عند التشغيل. اختر English داخل اللعبة لاستخدام التعريب. هل تريد المتابعة؟",
        ["PickGame"]="اختر مجلد اللعبة", ["PickPackage"]="اختر مجلد حزمة التعريب", ["OperationDone"]="اكتملت العملية.", ["InstallNotReady"]="التثبيت غير جاهز. راجع التعارضات أو الأخطاء أعلاه.",
        ["Transaction"]="العملية", ["NoActive"]="لا توجد عملية تثبيت نشطة.", ["Files"]="عدد الملفات", ["Unverified"]="غير مؤكد"
        , ["CompatibilityWarning"]="التوافق غير مؤكد. ملفات الإضافات غير موقعة، وهناك اختلافات في إصدارات التبعيات. يتطلب التثبيت تأكيداً صريحاً. يجب اختيار English داخل اللعبة."
    };
    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>
    {
        ["Title"]="Hollow Knight: Godmaster Arabic Launcher", ["Subtitle"]="Safely manage the bundled Arabic package",
        ["GamePath"]="Game folder", ["PackagePath"]="Translation package folder", ["Browse"]="Browse…", ["Refresh"]="Recheck",
        ["GameStatus"]="Game status", ["PackageStatus"]="Package status", ["Install"]="Install translation", ["Uninstall"]="Uninstall and restore",
        ["Recover"]="Recover interrupted operation", ["Launch"]="Launch game", ["Language"]="English / العربية", ["Compatibility"]="Compatibility",
        ["Log"]="Operation log", ["Ready"]="Ready to inspect", ["Warning"]="Compatibility is unverified; runtime DLLs are unsigned.",
        ["NoGame"]="No validated game installation.", ["NoPackage"]="Package validation failed.", ["Installed"]="Translation is installed and its files verified.",
        ["RecoverNone"]="No interrupted transaction.", ["Busy"]="Working…", ["Manual"]="Some files need manual review.",
        ["InstallConfirmTitle"]="Confirm translation component installation", ["InstallConfirm"]="The package includes runtime files that the game will load at startup. Continue only if you trust the package source.",
        ["UninstallConfirmTitle"]="Confirm translation removal", ["UninstallConfirm"]="Only files created by this transaction will be removed and the verified original backup restored. Files changed after installation will be left untouched.",
        ["RecoverConfirmTitle"]="Recover an interrupted operation", ["RecoverConfirm"]="The transaction journal will be checked and only changes matching this transaction's hashes will be rolled back. Continue?",
        ["LaunchConfirmTitle"]="Launch with unverified compatibility", ["LaunchConfirm"]="Runtime compatibility has not been confirmed. The game will load the installed plugins at startup. Select English in-game to use the translation. Continue?",
        ["PickGame"]="Select game folder", ["PickPackage"]="Select translation package folder", ["OperationDone"]="Operation completed.", ["InstallNotReady"]="Installation is not ready. Review conflicts and errors above.",
        ["Transaction"]="Transaction", ["NoActive"]="No active installation transaction.", ["Files"]="Files", ["Unverified"]="Unverified"
        , ["CompatibilityWarning"]="Compatibility is unverified. Runtime DLLs are unsigned and dependency versions differ. Installation requires explicit confirmation. Select English in-game."
    };
}

