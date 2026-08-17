// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace FlowMy.Core.Models.Media
{
    /// <summary>
    /// Helper nhận diện ngôn ngữ và cung cấp theme màu sắc chuẩn dark mode cho các thẻ phân loại ngôn ngữ (Language Tag Pills).
    /// </summary>
    public static class SubtitleLanguageHelper
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Brush> _brushCache = new(StringComparer.OrdinalIgnoreCase);

        public static Brush GetFrozenBrush(string? hex, string fallbackHex)
        {
            var key = string.IsNullOrWhiteSpace(hex) ? fallbackHex : hex;
            return _brushCache.GetOrAdd(key, k =>
            {
                try
                {
                    var brush = (Brush)new BrushConverter().ConvertFromString(k)!;
                    brush.Freeze();
                    return brush;
                }
                catch
                {
                    var fb = (Brush)new BrushConverter().ConvertFromString(fallbackHex)!;
                    fb.Freeze();
                    return fb;
                }
            });
        }

        public static (string tag, string name, string tagBg, string tagFg, string tagBorder, string textColor) GetLanguageInfo(string? langCode, bool isOriginal = false)
        {
            var code = (langCode ?? string.Empty).Trim().ToLowerInvariant();
            if (code.Contains("zh") || code.Contains("cn") || code.Contains("chi") || code.Contains("trung"))
                return ("CN", "Tiếng Trung", "#26F59E0B", "#FCD34D", "#50F59E0B", isOriginal ? "#FCD34D" : "#F8FAFC");
            if (code.Contains("vi") || code.Contains("vn") || code.Contains("viet") || code.Contains("việt"))
                return ("VN", "Tiếng Việt", "#266366F1", "#A5B4FC", "#406366F1", "#F1F5F9");
            if (code.Contains("en") || code.Contains("eng") || code.Contains("anh") || code.Contains("us") || code.Contains("gb"))
                return ("EN", "Tiếng Anh", "#2614B8A6", "#5EEAD4", "#4014B8A6", "#E2E8F0");
            if (code.Contains("ja") || code.Contains("jp") || code.Contains("jap") || code.Contains("nhật"))
                return ("JP", "Tiếng Nhật", "#26F43F5E", "#FDA4AF", "#40F43F5E", "#F8FAFC");
            if (code.Contains("ko") || code.Contains("kr") || code.Contains("kor") || code.Contains("hàn"))
                return ("KR", "Tiếng Hàn", "#26A855F7", "#D8B4FE", "#40A855F7", "#F8FAFC");
            if (code.Contains("fr") || code.Contains("pháp"))
                return ("FR", "Tiếng Pháp", "#263B82F6", "#93C5FD", "#403B82F6", "#F8FAFC");
            if (code.Contains("de") || code.Contains("đức"))
                return ("DE", "Tiếng Đức", "#26EAB308", "#FDE047", "#40EAB308", "#F8FAFC");
            if (code.Contains("es") || code.Contains("tây ban nha"))
                return ("ES", "Tây Ban Nha", "#26F97316", "#FDBA74", "#40F97316", "#F8FAFC");
            if (code.Contains("ru") || code.Contains("nga"))
                return ("RU", "Tiếng Nga", "#26EC4899", "#F472B6", "#40EC4899", "#F8FAFC");

            if (isOriginal)
                return ("GỐC", "Ngôn ngữ gốc", "#26F59E0B", "#FCD34D", "#50F59E0B", "#FCD34D");
            return ("DỊCH", "Bản dịch", "#266366F1", "#A5B4FC", "#406366F1", "#F1F5F9");
        }

        public static (string tag, string name, string tagBg, string tagFg, string tagBorder, string textColor) DetectLanguageFromText(string text, bool isOriginal = false)
        {
            if (string.IsNullOrWhiteSpace(text))
                return GetLanguageInfo(isOriginal ? "orig" : "trans", isOriginal);

            // Kiểm tra chữ Hán / CJK ideographs
            if (Regex.IsMatch(text, @"[\u4e00-\u9fa5\u3400-\u4dbf]"))
                return GetLanguageInfo("zh", isOriginal);

            // Kiểm tra Hiragana / Katakana tiếng Nhật
            if (Regex.IsMatch(text, @"[\u3040-\u309f\u30a0-\u30ff]"))
                return GetLanguageInfo("ja", isOriginal);

            // Kiểm tra Hangul tiếng Hàn
            if (Regex.IsMatch(text, @"[\uac00-\ud7af\u1100-\u11ff]"))
                return GetLanguageInfo("ko", isOriginal);

            // Kiểm tra tiếng Việt có dấu
            if (Regex.IsMatch(text, @"[àáảãạăắằẳẵặâấầẩẫậèéẻẽẹêếềểễệìíỉĩịòóỏõọôốồổỗộơớờởỡợùúủũụưứừửữựỳýỷỹỵđÀÁẢÃẠĂẮẰẲẴẶÂẤẦẨẪẬÈÉẺẸÊẾỀỂỄỆÌÍỈĨỊÒÓỎÕỌÔỐỒỔỖỘƠỚỜỞỠỢÙÚỦŨỤƯỨỪỬỮỰỲÝỶỸỴĐ]"))
                return GetLanguageInfo("vi", isOriginal);

            // Kiểm tra chữ Cyrillic tiếng Nga
            if (Regex.IsMatch(text, @"[\u0400-\u04ff]"))
                return GetLanguageInfo("ru", isOriginal);

            return isOriginal ? GetLanguageInfo("en", true) : GetLanguageInfo("vi", false);
        }
    }

    /// <summary>
    /// Thẻ ngôn ngữ trên thanh Filter / Toggle quản lý bật/tắt hiển thị từng bản dịch.
    /// </summary>
    public class SubtitleLanguageTag : INotifyPropertyChanged
    {
        private string _code = "zh";
        private string _name = "Tiếng Trung";
        private string _tag = "CN";
        private bool _isOriginal;
        private bool _isActive = true;
        private string _tagBackgroundHex = "#26F59E0B";
        private string _tagForegroundHex = "#FCD34D";
        private string _tagBorderHex = "#50F59E0B";
        private int _lineCount;

        public string Code { get => _code; set => SetField(ref _code, value ?? string.Empty); }
        public string Name { get => _name; set => SetField(ref _name, value ?? string.Empty); }
        public string Tag { get => _tag; set => SetField(ref _tag, value ?? string.Empty); }
        public bool IsOriginal { get => _isOriginal; set => SetField(ref _isOriginal, value); }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (SetField(ref _isActive, value))
                {
                    OnPropertyChanged(nameof(ActiveIndicatorText));
                    OnPropertyChanged(nameof(DisplayText));
                    OnPropertyChanged(nameof(StatusBrush));
                    OnPropertyChanged(nameof(BorderBrushDisplay));
                    OnPropertyChanged(nameof(BackgroundBrushDisplay));
                    OnPropertyChanged(nameof(ForegroundBrushDisplay));
                    OnPropertyChanged(nameof(OpacityDisplay));
                }
            }
        }

        public int LineCount
        {
            get => _lineCount;
            set
            {
                if (SetField(ref _lineCount, value))
                    OnPropertyChanged(nameof(DisplayText));
            }
        }

        public string TagBackgroundHex
        {
            get => _tagBackgroundHex;
            set
            {
                if (SetField(ref _tagBackgroundHex, value))
                    OnPropertyChanged(nameof(BackgroundBrushDisplay));
            }
        }

        public string TagForegroundHex
        {
            get => _tagForegroundHex;
            set
            {
                if (SetField(ref _tagForegroundHex, value))
                {
                    OnPropertyChanged(nameof(ForegroundBrushDisplay));
                    OnPropertyChanged(nameof(StatusBrush));
                }
            }
        }

        public string TagBorderHex
        {
            get => _tagBorderHex;
            set
            {
                if (SetField(ref _tagBorderHex, value))
                    OnPropertyChanged(nameof(BorderBrushDisplay));
            }
        }

        public string ActiveIndicatorText => _isActive ? "✓ " : "○ ";
        public string DisplayText => $"{ActiveIndicatorText}[{_tag}] {_name}{(IsOriginal ? " (Gốc)" : "")}";
        public double OpacityDisplay => _isActive ? 1.0 : 0.45;

        [JsonIgnore]
        public Brush StatusBrush => _isActive ? SubtitleLanguageHelper.GetFrozenBrush(_tagForegroundHex, "#FCD34D") : SubtitleLanguageHelper.GetFrozenBrush("#64748B", "#64748B");

        [JsonIgnore]
        public Brush BorderBrushDisplay => _isActive ? SubtitleLanguageHelper.GetFrozenBrush(_tagBorderHex, "#50F59E0B") : SubtitleLanguageHelper.GetFrozenBrush("#334155", "#334155");

        [JsonIgnore]
        public Brush BackgroundBrushDisplay => _isActive ? SubtitleLanguageHelper.GetFrozenBrush(_tagBackgroundHex, "#26F59E0B") : SubtitleLanguageHelper.GetFrozenBrush("#181926", "#181926");

        [JsonIgnore]
        public Brush ForegroundBrushDisplay => _isActive ? SubtitleLanguageHelper.GetFrozenBrush(_tagForegroundHex, "#FCD34D") : SubtitleLanguageHelper.GetFrozenBrush("#94A3B8", "#94A3B8");

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    /// <summary>
    /// Đại diện cho một dòng ngôn ngữ cụ thể (Bản gốc hoặc Bản dịch) trong một phân đoạn câu phụ đề.
    /// </summary>
    public class SubtitleLineItem : INotifyPropertyChanged
    {
        private string _languageCode = "vi";
        private string _languageName = "Tiếng Việt";
        private string _tag = "VN";
        private string _text = string.Empty;
        private bool _isOriginal;
        private bool _isActive = true;
        private string _tagBackgroundHex = "#266366F1";
        private string _tagForegroundHex = "#A5B4FC";
        private string _tagBorderHex = "#406366F1";
        private string _textColorHex = "#F1F5F9";

        [JsonIgnore]
        public Action? OnTextChangedAction { get; set; }

        public string LanguageCode
        {
            get => _languageCode;
            set => SetField(ref _languageCode, value ?? string.Empty);
        }

        public string LanguageName
        {
            get => _languageName;
            set => SetField(ref _languageName, value ?? string.Empty);
        }

        public string Tag
        {
            get => _tag;
            set => SetField(ref _tag, value ?? string.Empty);
        }

        public string Text
        {
            get => _text;
            set
            {
                if (SetField(ref _text, value ?? string.Empty))
                {
                    OnTextChangedAction?.Invoke();
                }
            }
        }

        public bool IsOriginal
        {
            get => _isOriginal;
            set => SetField(ref _isOriginal, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (SetField(ref _isActive, value))
                {
                    OnPropertyChanged(nameof(OpacityDisplay));
                    OnPropertyChanged(nameof(TagBackgroundBrush));
                    OnPropertyChanged(nameof(TagForegroundBrush));
                    OnPropertyChanged(nameof(TagBorderBrush));
                    OnPropertyChanged(nameof(TextColorBrush));
                }
            }
        }

        public double OpacityDisplay => _isActive ? 1.0 : 0.38;

        public string TagBackgroundHex
        {
            get => _tagBackgroundHex;
            set
            {
                if (SetField(ref _tagBackgroundHex, value))
                    OnPropertyChanged(nameof(TagBackgroundBrush));
            }
        }

        public string TagForegroundHex
        {
            get => _tagForegroundHex;
            set
            {
                if (SetField(ref _tagForegroundHex, value))
                    OnPropertyChanged(nameof(TagForegroundBrush));
            }
        }

        public string TagBorderHex
        {
            get => _tagBorderHex;
            set
            {
                if (SetField(ref _tagBorderHex, value))
                    OnPropertyChanged(nameof(TagBorderBrush));
            }
        }

        public string TextColorHex
        {
            get => _textColorHex;
            set
            {
                if (SetField(ref _textColorHex, value))
                    OnPropertyChanged(nameof(TextColorBrush));
            }
        }

        [JsonIgnore]
        public Brush TagBackgroundBrush => _isActive ? SubtitleLanguageHelper.GetFrozenBrush(_tagBackgroundHex, "#266366F1") : SubtitleLanguageHelper.GetFrozenBrush("#181926", "#181926");

        [JsonIgnore]
        public Brush TagForegroundBrush => _isActive ? SubtitleLanguageHelper.GetFrozenBrush(_tagForegroundHex, "#A5B4FC") : SubtitleLanguageHelper.GetFrozenBrush("#64748B", "#64748B");

        [JsonIgnore]
        public Brush TagBorderBrush => _isActive ? SubtitleLanguageHelper.GetFrozenBrush(_tagBorderHex, "#406366F1") : SubtitleLanguageHelper.GetFrozenBrush("#334155", "#334155");

        [JsonIgnore]
        public Brush TextColorBrush => _isActive ? SubtitleLanguageHelper.GetFrozenBrush(_textColorHex, "#F1F5F9") : SubtitleLanguageHelper.GetFrozenBrush("#64748B", "#64748B");

        public SubtitleLineItem Clone()
        {
            return new SubtitleLineItem
            {
                LanguageCode = LanguageCode,
                LanguageName = LanguageName,
                Tag = Tag,
                Text = Text,
                IsOriginal = IsOriginal,
                IsActive = IsActive,
                TagBackgroundHex = TagBackgroundHex,
                TagForegroundHex = TagForegroundHex,
                TagBorderHex = TagBorderHex,
                TextColorHex = TextColorHex
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public class SubtitleItem : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString("N");
        private double _startTimeSec;
        private double _endTimeSec = 2.0;
        private string _text = string.Empty;
        private string _originalText = string.Empty;
        private string _sourceLanguage = string.Empty;
        private bool _isSelected;
        private ObservableCollection<SubtitleLineItem> _lines = new();
        private Dictionary<string, string> _translations = new(StringComparer.OrdinalIgnoreCase);
        private bool _isSyncingLines;

        public SubtitleItem()
        {
            _lines.CollectionChanged += (s, e) =>
            {
                HookLinesEvents();
                SyncTextFromLines();
                OnPropertyChanged(nameof(HasMultipleLines));
                OnPropertyChanged(nameof(BilingualSummary));
                OnPropertyChanged(nameof(ActiveLines));
                OnPropertyChanged(nameof(HasActiveLines));
            };
        }

        public string Id
        {
            get => _id;
            set => SetField(ref _id, value);
        }

        public double StartTimeSec
        {
            get => _startTimeSec;
            set
            {
                if (SetField(ref _startTimeSec, Math.Max(0, value)))
                {
                    OnPropertyChanged(nameof(FormattedStartTime));
                    OnPropertyChanged(nameof(StartTimeHms));
                    OnPropertyChanged(nameof(FormattedTimeRange));
                    OnPropertyChanged(nameof(DurationSec));
                }
            }
        }

        public double EndTimeSec
        {
            get => _endTimeSec;
            set
            {
                if (SetField(ref _endTimeSec, Math.Max(0, value)))
                {
                    OnPropertyChanged(nameof(FormattedEndTime));
                    OnPropertyChanged(nameof(EndTimeHms));
                    OnPropertyChanged(nameof(FormattedTimeRange));
                    OnPropertyChanged(nameof(DurationSec));
                }
            }
        }

        public double DurationSec => Math.Max(0, _endTimeSec - _startTimeSec);

        public string Text
        {
            get => _text;
            set
            {
                if (SetField(ref _text, value ?? string.Empty))
                {
                    if (!_isSyncingLines && _lines.Count == 0 && !string.IsNullOrWhiteSpace(_text))
                    {
                        EnsureLinesFromText();
                    }
                }
            }
        }

        public string OriginalText
        {
            get => _originalText;
            set => SetField(ref _originalText, value ?? string.Empty);
        }

        public string SourceLanguage
        {
            get => _sourceLanguage;
            set => SetField(ref _sourceLanguage, value ?? string.Empty);
        }

        public Dictionary<string, string> Translations
        {
            get => _translations;
            set => SetField(ref _translations, value ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        public ObservableCollection<SubtitleLineItem> Lines
        {
            get
            {
                if (_lines.Count == 0 && (!string.IsNullOrWhiteSpace(_text) || !string.IsNullOrWhiteSpace(_originalText) || _translations.Count > 0))
                {
                    EnsureLinesFromText();
                }
                return _lines;
            }
            set
            {
                if (SetField(ref _lines, value ?? new ObservableCollection<SubtitleLineItem>()))
                {
                    HookLinesEvents();
                    SyncTextFromLines();
                    OnPropertyChanged(nameof(HasMultipleLines));
                    OnPropertyChanged(nameof(BilingualSummary));
                    OnPropertyChanged(nameof(ActiveLines));
                    OnPropertyChanged(nameof(HasActiveLines));
                }
            }
        }

        public bool HasMultipleLines => Lines.Count > 1;
        public IEnumerable<SubtitleLineItem> ActiveLines => Lines.Where(l => l.IsActive);
        public bool HasActiveLines => Lines.Any(l => l.IsActive);

        public string BilingualSummary
        {
            get
            {
                var activeList = Lines.Where(l => l.IsActive).ToList();
                if (activeList.Count >= 2)
                    return $"{activeList[0].Tag} ➔ {activeList[1].Tag}";
                if (activeList.Count == 1)
                    return activeList[0].Tag;
                if (Lines.Count >= 2)
                    return $"{Lines[0].Tag} ➔ {Lines[1].Tag}";
                if (Lines.Count == 1)
                    return Lines[0].Tag;
                return "SUB";
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetField(ref _isSelected, value);
        }

        public string FormattedStartTime => FormatHms(_startTimeSec);
        public string FormattedEndTime => FormatHms(_endTimeSec);

        public string StartTimeHms
        {
            get => FormatHms(_startTimeSec);
            set
            {
                if (TryParseHms(value, out var sec) && Math.Abs(sec - _startTimeSec) > 0.001)
                {
                    StartTimeSec = sec;
                }
            }
        }

        public string EndTimeHms
        {
            get => FormatHms(_endTimeSec);
            set
            {
                if (TryParseHms(value, out var sec) && Math.Abs(sec - _endTimeSec) > 0.001)
                {
                    EndTimeSec = sec;
                }
            }
        }

        public string FormattedTimeRange => $"{FormatHms(_startTimeSec)} ➔ {FormatHms(_endTimeSec)} ({DurationSec:0.0}s)";

        public void HookLinesEvents()
        {
            foreach (var line in _lines)
            {
                line.OnTextChangedAction = () =>
                {
                    SyncTextFromLines();
                };
            }
        }

        public void EnsureLinesFromText()
        {
            if (_isSyncingLines) return;
            _isSyncingLines = true;
            try
            {
                _lines.Clear();

                // 1. Nếu có translations map
                if (_translations != null && _translations.Count > 0)
                {
                    var sourceCode = !string.IsNullOrWhiteSpace(_sourceLanguage) ? _sourceLanguage : "zh";
                    // Thêm bản gốc trước nếu có
                    if (_translations.TryGetValue(sourceCode, out var origVal) && !string.IsNullOrWhiteSpace(origVal))
                    {
                        var info = SubtitleLanguageHelper.GetLanguageInfo(sourceCode, isOriginal: true);
                        _lines.Add(new SubtitleLineItem
                        {
                            LanguageCode = sourceCode,
                            LanguageName = info.name,
                            Tag = info.tag,
                            Text = origVal,
                            IsOriginal = true,
                            IsActive = true,
                            TagBackgroundHex = info.tagBg,
                            TagForegroundHex = info.tagFg,
                            TagBorderHex = info.tagBorder,
                            TextColorHex = info.textColor
                        });
                    }

                    // Thêm các bản dịch còn lại
                    foreach (var kvp in _translations)
                    {
                        if (string.Equals(kvp.Key, sourceCode, StringComparison.OrdinalIgnoreCase)) continue;
                        var info = SubtitleLanguageHelper.GetLanguageInfo(kvp.Key, isOriginal: false);
                        _lines.Add(new SubtitleLineItem
                        {
                            LanguageCode = kvp.Key,
                            LanguageName = info.name,
                            Tag = info.tag,
                            Text = kvp.Value,
                            IsOriginal = false,
                            IsActive = true,
                            TagBackgroundHex = info.tagBg,
                            TagForegroundHex = info.tagFg,
                            TagBorderHex = info.tagBorder,
                            TextColorHex = info.textColor
                        });
                    }
                }
                // 2. Nếu có OriginalText và Text tách biệt
                else if (!string.IsNullOrWhiteSpace(_originalText) && !string.IsNullOrWhiteSpace(_text) && _originalText != _text)
                {
                    var origInfo = SubtitleLanguageHelper.DetectLanguageFromText(_originalText, isOriginal: true);
                    var transInfo = SubtitleLanguageHelper.DetectLanguageFromText(_text, isOriginal: false);

                    _lines.Add(new SubtitleLineItem
                    {
                        LanguageCode = origInfo.tag.ToLowerInvariant(),
                        LanguageName = origInfo.name,
                        Tag = origInfo.tag,
                        Text = _originalText,
                        IsOriginal = true,
                        IsActive = true,
                        TagBackgroundHex = origInfo.tagBg,
                        TagForegroundHex = origInfo.tagFg,
                        TagBorderHex = origInfo.tagBorder,
                        TextColorHex = origInfo.textColor
                    });

                    _lines.Add(new SubtitleLineItem
                    {
                        LanguageCode = transInfo.tag.ToLowerInvariant(),
                        LanguageName = transInfo.name,
                        Tag = transInfo.tag,
                        Text = _text,
                        IsOriginal = false,
                        IsActive = true,
                        TagBackgroundHex = transInfo.tagBg,
                        TagForegroundHex = transInfo.tagFg,
                        TagBorderHex = transInfo.tagBorder,
                        TextColorHex = transInfo.textColor
                    });
                }
                // 3. Phân tích từ Text (nếu có nhiều dòng)
                else if (!string.IsNullOrWhiteSpace(_text))
                {
                    var split = _text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
                    if (split.Length >= 2)
                    {
                        var origInfo = SubtitleLanguageHelper.DetectLanguageFromText(split[0], isOriginal: true);
                        _lines.Add(new SubtitleLineItem
                        {
                            LanguageCode = origInfo.tag.ToLowerInvariant(),
                            LanguageName = origInfo.name,
                            Tag = origInfo.tag,
                            Text = split[0],
                            IsOriginal = true,
                            IsActive = true,
                            TagBackgroundHex = origInfo.tagBg,
                            TagForegroundHex = origInfo.tagFg,
                            TagBorderHex = origInfo.tagBorder,
                            TextColorHex = origInfo.textColor
                        });

                        for (int i = 1; i < split.Length; i++)
                        {
                            var transInfo = SubtitleLanguageHelper.DetectLanguageFromText(split[i], isOriginal: false);
                            _lines.Add(new SubtitleLineItem
                            {
                                LanguageCode = transInfo.tag.ToLowerInvariant(),
                                LanguageName = transInfo.name,
                                Tag = transInfo.tag,
                                Text = split[i],
                                IsOriginal = false,
                                IsActive = true,
                                TagBackgroundHex = transInfo.tagBg,
                                TagForegroundHex = transInfo.tagFg,
                                TagBorderHex = transInfo.tagBorder,
                                TextColorHex = transInfo.textColor
                            });
                        }
                    }
                    else
                    {
                        var info = SubtitleLanguageHelper.DetectLanguageFromText(_text, isOriginal: false);
                        _lines.Add(new SubtitleLineItem
                        {
                            LanguageCode = info.tag.ToLowerInvariant(),
                            LanguageName = info.name,
                            Tag = info.tag,
                            Text = _text,
                            IsOriginal = false,
                            IsActive = true,
                            TagBackgroundHex = info.tagBg,
                            TagForegroundHex = info.tagFg,
                            TagBorderHex = info.tagBorder,
                            TextColorHex = info.textColor
                        });
                    }
                }
                else
                {
                    var info = SubtitleLanguageHelper.GetLanguageInfo("vi", isOriginal: false);
                    _lines.Add(new SubtitleLineItem
                    {
                        LanguageCode = "vi",
                        LanguageName = info.name,
                        Tag = info.tag,
                        Text = string.Empty,
                        IsOriginal = false,
                        IsActive = true,
                        TagBackgroundHex = info.tagBg,
                        TagForegroundHex = info.tagFg,
                        TagBorderHex = info.tagBorder,
                        TextColorHex = info.textColor
                    });
                }

                HookLinesEvents();
            }
            finally
            {
                _isSyncingLines = false;
            }
        }

        private void SyncTextFromLines()
        {
            if (_isSyncingLines) return;
            _isSyncingLines = true;
            try
            {
                if (_lines.Count == 0) return;

                _translations ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                if (_lines.Count >= 2)
                {
                    _originalText = _lines[0].Text;
                    _text = _lines[1].Text;

                    foreach (var l in _lines)
                    {
                        if (!string.IsNullOrWhiteSpace(l.LanguageCode))
                            _translations[l.LanguageCode] = l.Text;
                    }
                }
                else if (_lines.Count == 1)
                {
                    if (_lines[0].IsOriginal)
                    {
                        _originalText = _lines[0].Text;
                        _text = _lines[0].Text;
                    }
                    else
                    {
                        _text = _lines[0].Text;
                    }
                    if (!string.IsNullOrWhiteSpace(_lines[0].LanguageCode))
                        _translations[_lines[0].LanguageCode] = _lines[0].Text;
                }

                OnPropertyChanged(nameof(Text));
                OnPropertyChanged(nameof(OriginalText));
                OnPropertyChanged(nameof(HasMultipleLines));
                OnPropertyChanged(nameof(BilingualSummary));
                OnPropertyChanged(nameof(ActiveLines));
                OnPropertyChanged(nameof(HasActiveLines));
            }
            finally
            {
                _isSyncingLines = false;
            }
        }

        public static string FormatHms(double sec)
        {
            if (double.IsNaN(sec) || double.IsInfinity(sec) || sec < 0) sec = 0;
            var ts = TimeSpan.FromSeconds(sec);
            int hours = (int)ts.TotalHours;
            int mins = ts.Minutes;
            int secs = ts.Seconds;
            int tenths = (ts.Milliseconds / 100);
            return $"{hours:00}:{mins:00}:{secs:00}.{tenths:0}";
        }

        public static bool TryParseHms(string? input, out double seconds)
        {
            seconds = 0;
            if (string.IsNullOrWhiteSpace(input)) return false;
            input = input.Trim().Replace(',', '.');

            if (double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var plainSec))
            {
                seconds = Math.Max(0, plainSec);
                return true;
            }

            var parts = input.Split(':');
            try
            {
                if (parts.Length == 3)
                {
                    if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var h) &&
                        double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var m) &&
                        double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var s))
                    {
                        seconds = Math.Max(0, h * 3600 + m * 60 + s);
                        return true;
                    }
                }
                else if (parts.Length == 2)
                {
                    if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var m) &&
                        double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var s))
                    {
                        seconds = Math.Max(0, m * 60 + s);
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        public SubtitleItem Clone()
        {
            var item = new SubtitleItem
            {
                Id = Guid.NewGuid().ToString("N"),
                StartTimeSec = StartTimeSec,
                EndTimeSec = EndTimeSec,
                Text = Text,
                OriginalText = OriginalText,
                SourceLanguage = SourceLanguage,
                Translations = new Dictionary<string, string>(Translations)
            };

            item.Lines.Clear();
            foreach (var line in Lines)
            {
                item.Lines.Add(line.Clone());
            }
            item.HookLinesEvents();
            return item;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public class SubtitleStyleConfig : INotifyPropertyChanged
    {
        private bool _enabled = true;
        private bool _hardcodeBurnIn = true;
        private string _fontFamily = "Segoe UI";
        private double _fontSize = 24;
        private bool _isBold = true;
        private bool _isItalic;
        private string _textColor = "#FFFFFF";
        private string _highlightColor = "#FFD700";
        private double _outlineThickness = 2.0;
        private string _outlineColor = "#000000";
        private double _shadowDistance = 1.5;
        private string _shadowColor = "#80000000";
        private bool _backgroundBoxEnabled;
        private string _backgroundBoxColor = "#A0000000";
        private double _backgroundBoxPadding = 6;
        private double _backgroundBoxCornerRadius = 4;
        private string _alignment = "BottomCenter";
        private double _bottomMarginPx = 40;
        private double _sideMarginPx = 20;
        private string _animationEffect = "None";
        private string _presetTheme = "Default";
        private bool _autoWrapLongText = true;
        private int _maxCharsPerLine = 36;
        private double _positionOffsetX;
        private double _positionOffsetY;

        public bool Enabled { get => _enabled; set => SetField(ref _enabled, value); }
        public bool HardcodeBurnIn { get => _hardcodeBurnIn; set => SetField(ref _hardcodeBurnIn, value); }
        public string FontFamily { get => _fontFamily; set => SetField(ref _fontFamily, value); }
        public double FontSize { get => _fontSize; set => SetField(ref _fontSize, value); }
        public bool IsBold { get => _isBold; set => SetField(ref _isBold, value); }
        public bool IsItalic { get => _isItalic; set => SetField(ref _isItalic, value); }
        public string TextColor { get => _textColor; set => SetField(ref _textColor, value); }
        public string HighlightColor { get => _highlightColor; set => SetField(ref _highlightColor, value); }
        public double OutlineThickness { get => _outlineThickness; set => SetField(ref _outlineThickness, value); }
        public string OutlineColor { get => _outlineColor; set => SetField(ref _outlineColor, value); }
        public double ShadowDistance { get => _shadowDistance; set => SetField(ref _shadowDistance, value); }
        public string ShadowColor { get => _shadowColor; set => SetField(ref _shadowColor, value); }
        public bool BackgroundBoxEnabled { get => _backgroundBoxEnabled; set => SetField(ref _backgroundBoxEnabled, value); }
        public string BackgroundBoxColor { get => _backgroundBoxColor; set => SetField(ref _backgroundBoxColor, value); }
        public double BackgroundBoxPadding { get => _backgroundBoxPadding; set => SetField(ref _backgroundBoxPadding, value); }
        public double BackgroundBoxCornerRadius { get => _backgroundBoxCornerRadius; set => SetField(ref _backgroundBoxCornerRadius, value); }
        public string Alignment { get => _alignment; set => SetField(ref _alignment, value); }
        public double BottomMarginPx { get => _bottomMarginPx; set => SetField(ref _bottomMarginPx, value); }
        public double SideMarginPx { get => _sideMarginPx; set => SetField(ref _sideMarginPx, value); }
        public double PositionOffsetX { get => _positionOffsetX; set => SetField(ref _positionOffsetX, value); }
        public double PositionOffsetY { get => _positionOffsetY; set => SetField(ref _positionOffsetY, value); }
        public string AnimationEffect { get => _animationEffect; set => SetField(ref _animationEffect, value); }
        public string PresetTheme { get => _presetTheme; set => SetField(ref _presetTheme, value); }
        public bool AutoWrapLongText { get => _autoWrapLongText; set => SetField(ref _autoWrapLongText, value); }
        public int MaxCharsPerLine { get => _maxCharsPerLine; set => SetField(ref _maxCharsPerLine, value); }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public static class SubtitleAssBuilder
    {
        public static string BuildAssFileContent(
            IEnumerable<SubtitleItem> subtitles,
            SubtitleStyleConfig? style,
            int sourceWidth,
            int sourceHeight)
        {
            var sb = new System.Text.StringBuilder();
            var font = !string.IsNullOrWhiteSpace(style?.FontFamily) ? style.FontFamily : "Segoe UI";

            int playResX = sourceWidth > 0 ? sourceWidth : 1920;
            int playResY = sourceHeight > 0 ? sourceHeight : 1080;
            double minDim = Math.Min(playResX, playResY);

            double userFontSize = style?.FontSize ?? 24;
            double videoFontSize = Math.Max(12.0, (userFontSize * 2.25) * (minDim / 1080.0));
            int assFontSize = (int)Math.Round(videoFontSize);
            int assFontSizeOrig = (int)Math.Round(videoFontSize * 0.85);

            int wrapStyle = (style?.AutoWrapLongText ?? true) ? 0 : 2;

            sb.AppendLine("[Script Info]");
            sb.AppendLine("Title: FlowMy Subtitles");
            sb.AppendLine("ScriptType: v4.00+");
            sb.AppendLine($"PlayResX: {playResX}");
            sb.AppendLine($"PlayResY: {playResY}");
            sb.AppendLine($"WrapStyle: {wrapStyle}");
            sb.AppendLine("ScaledBorderAndShadow: yes");
            sb.AppendLine();

            sb.AppendLine("[V4+ Styles]");
            sb.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");

            var boldVal = (style?.IsBold ?? true) ? -1 : 0;
            var italicVal = (style?.IsItalic ?? false) ? -1 : 0;

            var alignStr = style?.Alignment ?? "BottomCenter";
            var alignVal = alignStr switch
            {
                "TopLeft" => 7,
                "TopCenter" => 8,
                "TopRight" => 9,
                "CenterLeft" => 4,
                "Center" => 5,
                "CenterRight" => 6,
                "BottomLeft" => 1,
                "BottomRight" => 3,
                _ => 2 // BottomCenter
            };

            var primaryColor = ColorToAssHex(style?.TextColor ?? "#FFFFFF", 0x00);
            var origColor = "&H00FCD34D"; // Gold/Amber for Original text in ASS

            int borderStyle = (style != null && style.BackgroundBoxEnabled) ? 3 : 1;
            string outlineColor;
            string backColor;
            int outlineThick;
            int shadowDist;

            if (borderStyle == 3)
            {
                outlineColor = ColorToAssHex(style?.BackgroundBoxColor ?? "#A0000000", 0x60);
                backColor = "&HFF000000";
                outlineThick = Math.Max(6, (int)Math.Round(videoFontSize * 0.38));
                shadowDist = 0;
            }
            else
            {
                outlineColor = ColorToAssHex(style?.OutlineColor ?? "#000000", 0x00);
                backColor = ColorToAssHex(style?.ShadowColor ?? "#80000000", 0x80);
                outlineThick = Math.Max(0, (int)Math.Round(((style?.OutlineThickness ?? 2) * 2.0) * (minDim / 1080.0)));
                shadowDist = Math.Max(0, (int)Math.Round(((style?.ShadowDistance ?? 1) * 2.0) * (minDim / 1080.0)));
            }

            double sMargin = Math.Max(30.0, playResX * 0.05) * (minDim / 1080.0);
            double bMargin = ((style?.BottomMarginPx ?? 40) * 1.5) * (minDim / 1080.0);
            double offX = ((style?.PositionOffsetX ?? 0) * 1.5) * (minDim / 1080.0);
            double offY = ((style?.PositionOffsetY ?? 0) * 1.5) * (minDim / 1080.0);

            int marginL = (int)Math.Max(0, Math.Round(sMargin));
            int marginR = (int)Math.Max(0, Math.Round(sMargin));
            int marginV = (int)Math.Max(0, Math.Round(bMargin));
            int marginVTranslated = (int)Math.Max(0, Math.Round(bMargin + videoFontSize * 1.25));

            sb.AppendLine($"Style: Default,{font},{assFontSizeOrig},{origColor},&H000000FF,{outlineColor},{backColor},{boldVal},{italicVal},0,0,100,100,0,0,{borderStyle},{outlineThick},{shadowDist},{alignVal},{marginL},{marginR},{marginV},1");
            sb.AppendLine($"Style: Translated,{font},{assFontSize},{primaryColor},&H000000FF,{outlineColor},{backColor},{boldVal},{italicVal},0,0,100,100,0,0,{borderStyle},{outlineThick},{shadowDist},{alignVal},{marginL},{marginR},{marginVTranslated},1");
            sb.AppendLine();

            sb.AppendLine("[Events]");
            sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

            double posX = (playResX / 2.0) + offX;
            double posY = playResY - bMargin + offY;

            var posTag = $"\\an{alignVal}\\pos({posX.ToString("0.#", CultureInfo.InvariantCulture)},{posY.ToString("0.#", CultureInfo.InvariantCulture)})";

            foreach (var sub in subtitles)
            {
                var st = TimeSpan.FromSeconds(Math.Max(0, sub.StartTimeSec));
                var et = TimeSpan.FromSeconds(Math.Max(sub.StartTimeSec + 0.1, sub.EndTimeSec));
                var stStr = $"{(int)st.TotalHours:0}:{st.Minutes:00}:{st.Seconds:00}.{st.Milliseconds / 10:00}";
                var etStr = $"{(int)et.TotalHours:0}:{et.Minutes:00}:{et.Seconds:00}.{et.Milliseconds / 10:00}";

                var activeLines = sub.Lines.Where(l => l.IsActive).ToList();
                if (activeLines.Count == 0)
                {
                    continue;
                }
                else if (activeLines.Count == 1)
                {
                    var l = activeLines[0];
                    var styleName = l.IsOriginal ? "Default" : "Translated";
                    var txt = (l.Text ?? string.Empty).Replace("\r\n", @"\N").Replace("\n", @"\N").Replace("\r", @"\N");
                    sb.AppendLine($"Dialogue: 0,{stStr},{etStr},{styleName},,0,0,0,,{{{posTag}}}{txt}");
                }
                else if (activeLines.Count == 2)
                {
                    // Dual-line bilingual mode (Translated on top, Original on bottom)
                    var origLine = activeLines.FirstOrDefault(l => l.IsOriginal) ?? activeLines[0];
                    var transLine = activeLines.FirstOrDefault(l => !l.IsOriginal) ?? activeLines[1];

                    var origText = origLine.Text.Replace("\r\n", @"\N").Replace("\n", @"\N").Replace("\r", @"\N");
                    var transText = transLine.Text.Replace("\r\n", @"\N").Replace("\n", @"\N").Replace("\r", @"\N");
                    sb.AppendLine($"Dialogue: 0,{stStr},{etStr},Translated,,0,0,0,,{transText}");
                    sb.AppendLine($"Dialogue: 0,{stStr},{etStr},Default,,0,0,0,,{origText}");
                }
                else
                {
                    // 3+ active lines
                    for (int i = 0; i < activeLines.Count; i++)
                    {
                        var l = activeLines[i];
                        var styleName = l.IsOriginal ? "Default" : "Translated";
                        var txt = (l.Text ?? string.Empty).Replace("\r\n", @"\N").Replace("\n", @"\N").Replace("\r", @"\N");
                        sb.AppendLine($"Dialogue: 0,{stStr},{etStr},{styleName},,0,0,0,,{txt}");
                    }
                }
            }

            return sb.ToString();
        }

        public static string ColorToAssHex(string hexColor, byte defaultAssAlpha = 0x00)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hexColor)) return "&H00FFFFFF";
                var cleaned = hexColor.Trim().TrimStart('#');
                byte a = defaultAssAlpha, r = 255, g = 255, b = 255;
                if (cleaned.Length == 6)
                {
                    r = Convert.ToByte(cleaned.Substring(0, 2), 16);
                    g = Convert.ToByte(cleaned.Substring(2, 2), 16);
                    b = Convert.ToByte(cleaned.Substring(4, 2), 16);
                    a = defaultAssAlpha;
                }
                else if (cleaned.Length == 8)
                {
                    var wpfAlpha = Convert.ToByte(cleaned.Substring(0, 2), 16);
                    a = (byte)(255 - wpfAlpha);
                    r = Convert.ToByte(cleaned.Substring(2, 2), 16);
                    g = Convert.ToByte(cleaned.Substring(4, 2), 16);
                    b = Convert.ToByte(cleaned.Substring(6, 2), 16);
                }
                return $"&H{a:X2}{b:X2}{g:X2}{r:X2}";
            }
            catch
            {
                return "&H00FFFFFF";
            }
        }
    }
}
