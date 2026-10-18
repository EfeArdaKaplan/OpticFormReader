using System.Globalization;
using System.Security;
using System.Text;
using core.Entities;
using core.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace business.Services
{
    public class FormGeneratorService
    {
        // Özel alanların SectionId değerleri (okuma motoru da bunları kullanacak).
        public const int StudentNumberSectionId = -1;
        public const int TcNumberSectionId = -2;
        public const int BookletSectionId = -3;
        public const int NameSectionId = -4;

        // ---------- Sayfa yerleşimi (mm) ----------
        private const double PageLeft = 10;
        private const double PageWidth = 190;
        private const double TopBlockY = 30;
        private const double BlockGap = 4;

        // Frontend'deki 1.9fr / 1.1fr oranı.
        private const double LeftBlockWidth = (PageWidth - BlockGap) * 1.9 / 3.0;
        private const double RightBlockX = PageLeft + LeftBlockWidth + BlockGap;
        private const double RightBlockWidth = PageWidth - LeftBlockWidth - BlockGap;

        // Sol blok
        private const double HandwritingY = TopBlockY;
        private const double HandwritingHeight = 15;
        private const double BookletY = HandwritingY + HandwritingHeight + 2;
        private const double BookletHeight = 8;
        private const double NameY = BookletY + BookletHeight + 2;

        // Kodlama ızgaraları (ad-soyad, öğrenci no, T.C.)
        private const double GridBoxTop = 6.6;
        private const double GridBoxSize = 3.6;
        private const double GridBoxGap = 0.8;
        private const double GridBubbleRadius = 1.25;
        private const double GridBottomPad = 1.5;
        private const double GridFirstCenter =
            GridBoxTop + GridBoxSize + GridBoxGap + GridBubbleRadius;

        private const int NameColumns = 25;
        private const double NameRowPitch = 2.95;
        private const double DigitRowPitch = 3.0;
        private const double DigitColumnPitch = 4.4;
        private const double BookletBubbleRadius = 1.5;

        private const double DigitGridHeight =
            GridFirstCenter + 9 * DigitRowPitch + GridBubbleRadius + GridBottomPad;

        private const double NameGridHeight =
            GridFirstCenter + 27 * NameRowPitch + GridBubbleRadius + GridBottomPad;

        // Sağ blok
        private const double InstructionsY = TopBlockY + DigitGridHeight + 2;
        private const double InstructionsHeight = 22;
        private const double TcY = InstructionsY + InstructionsHeight + 2;

        // Soru alanı
        private const double SeparatorY = 154.5;
        private const double QuestionsTop = 157;
        private const double QuestionsBottom = 282;
        private const double ColumnBottomPad = 1.0;
        private const int MaxColumns = 5;
        private const double ColumnWidth = 35.6;
        private const double ColumnStep = 38.6;
        private const double SegmentHeaderHeight = 6.0;
        private const double SegmentGap = 0.8;
        private const double RowPitch = 3.6;
        private const double QuestionBubbleRadius = 1.35;
        private const double QuestionNumberOffset = 8.2;
        private const double FirstOptionOffset = 11.5;
        private const double OptionPitch = 4.6;

        private static readonly string[] NameLetters =
        {
            "A", "B", "C", "Ç", "D", "E", "F", "G", "H", "I", "İ", "J", "K", "L",
            "M", "N", "O", "Ö", "P", "R", "S", "Ş", "T", "U", "Ü", "V", "Y", "Z"
        };

        private const string Options = "ABCDE";

        private static readonly CultureInfo Turkish = new CultureInfo("tr-TR");

        private sealed record Segment(
            ExamSection Section, int Start, int End, int Column, double Y);

        // =====================================================================
        // 1) HARİTA ÜRETİMİ
        // =====================================================================
        public FormLayout BuildLayout(ExamTemplate template)
        {
            ArgumentNullException.ThrowIfNull(template);

            var sections = template.Sections
                .OrderBy(s => s.Order)
                .ToList();

            if (sections.Count == 0)
            {
                throw new InvalidOperationException(
                    "Form oluşturmak için en az bir ders bulunmalıdır.");
            }

            if (template.StudentNoDigits < 1 || template.StudentNoDigits > 11)
            {
                throw new InvalidOperationException(
                    "Öğrenci numarası 1 ile 11 hane arasında olmalıdır.");
            }

            foreach (var section in sections)
            {
                if (section.QuestionCount < 1 ||
                    section.OptionCount < 2 ||
                    section.OptionCount > 5)
                {
                    throw new InvalidOperationException(
                        $"{section.Name} dersi için soru veya şık sayısı geçersiz.");
                }
            }

            int totalQuestions = sections.Sum(s => s.QuestionCount);

            // Önce her ders kendi sütununda başlasın (frontend'deki gibi),
            // sığmazsa dersleri alt alta sıkıştır.
            var segments = TryPlace(sections, newColumnPerSection: true)
                           ?? TryPlace(sections, newColumnPerSection: false);

            if (segments == null)
            {
                throw new InvalidOperationException(
                    "Ders ve soru sayısı tek A4 sayfasına sığmıyor (en fazla 150 soru).");
            }

            var layout = new FormLayout
            {
                TemplateId = template.Id,
                InstitutionName = template.InstitutionName,
                FormTitle = template.Name,
                StudentNoDigits = template.StudentNoDigits,
                TotalQuestionCount = totalQuestions
            };

            AddMarkers(layout);
            AddHeader(layout, template, totalQuestions, sections.Count);
            AddHandwritingAndBooklet(layout);
            AddNameGrid(layout);

            AddDigitGrid(
                layout, "StudentNumber",
                $"ÖĞRENCİ NUMARASI ({template.StudentNoDigits} HANE)",
                StudentNumberSectionId, TopBlockY, template.StudentNoDigits);

            AddInstructions(layout);

            AddDigitGrid(
                layout, "TcNumber",
                "T.C. KİMLİK NUMARASI (11 HANE)",
                TcNumberSectionId, TcY, 11);

            AddQuestionColumns(layout, segments);

            return layout;
        }

        private static void AddMarkers(FormLayout layout)
        {
            // Sol üst köşe diğerlerinden büyük: ters yüklenen sayfayı bulmak için.
            layout.Markers.Add(new MarkerInfo
            { Xmm = 5, Ymm = 5, SizeMm = 12, Role = "TopLeft" });
            layout.Markers.Add(new MarkerInfo
            { Xmm = 197, Ymm = 5, SizeMm = 8, Role = "TopRight" });
            layout.Markers.Add(new MarkerInfo
            { Xmm = 5, Ymm = 284, SizeMm = 8, Role = "BottomLeft" });
            layout.Markers.Add(new MarkerInfo
            { Xmm = 197, Ymm = 284, SizeMm = 8, Role = "BottomRight" });
        }

        private static void AddHeader(
            FormLayout layout, ExamTemplate template, int totalQuestions, int sectionCount)
        {
            layout.Fields.Add(new FieldInfo
            {
                Name = "InstitutionName",
                Label = template.InstitutionName.ToUpper(Turkish),
                Type = "CenteredText",
                Xmm = PageLeft,
                Ymm = 9,
                WidthMm = PageWidth,
                HeightMm = 7
            });

            layout.Fields.Add(new FieldInfo
            {
                Name = "FormTitle",
                Label = $"{template.Name.ToUpper(Turkish)} OPTİK CEVAP FORMU",
                Type = "CenteredText",
                Xmm = PageLeft,
                Ymm = 16,
                WidthMm = PageWidth,
                HeightMm = 6
            });

            layout.Fields.Add(new FieldInfo
            {
                Name = "Subtitle",
                Label = $"Toplam {totalQuestions} Soru | {sectionCount} Ders",
                Type = "CenteredText",
                Xmm = PageLeft,
                Ymm = 22,
                WidthMm = PageWidth,
                HeightMm = 4
            });
        }

        private static void AddHandwritingAndBooklet(FormLayout layout)
        {
            layout.Fields.Add(new FieldInfo
            {
                Name = "HandwritingBox",
                Label = "El yazısı alanı",
                Type = "HandwritingBox",
                Xmm = PageLeft,
                Ymm = HandwritingY,
                WidthMm = LeftBlockWidth,
                HeightMm = HandwritingHeight
            });

            layout.Fields.Add(new FieldInfo
            {
                Name = "Booklet",
                Label = "KİTAPÇIK TÜRÜ:",
                Type = "BookletBox",
                Xmm = PageLeft,
                Ymm = BookletY,
                WidthMm = LeftBlockWidth,
                HeightMm = BookletHeight,
                CharacterCount = 1
            });

            double right = PageLeft + LeftBlockWidth;
            string[] booklets = { "A", "B", "C", "D" };

            for (int i = 0; i < booklets.Length; i++)
            {
                layout.Bubbles.Add(new BubbleInfo
                {
                    SectionId = BookletSectionId,
                    QuestionNumber = 1,
                    Option = booklets[i],
                    Xmm = right - 5.5 - (booklets.Length - 1 - i) * 6,
                    Ymm = BookletY + BookletHeight / 2,
                    RadiusMm = BookletBubbleRadius
                });
            }
        }

        private static void AddNameGrid(FormLayout layout)
        {
            layout.Fields.Add(new FieldInfo
            {
                Name = "NameGrid",
                Label = "AD VE SOYAD KODLAMA ALANI (25 KARAKTER - ÖSYM FORMATI)",
                Type = "DigitGrid",
                Xmm = PageLeft,
                Ymm = NameY,
                WidthMm = LeftBlockWidth,
                HeightMm = NameGridHeight,
                CharacterCount = NameColumns
            });

            double pitch = (LeftBlockWidth - 4) / NameColumns;

            for (int position = 1; position <= NameColumns; position++)
            {
                double x = PageLeft + 2 + (position - 0.5) * pitch;

                for (int i = 0; i < NameLetters.Length; i++)
                {
                    layout.Bubbles.Add(new BubbleInfo
                    {
                        SectionId = NameSectionId,
                        QuestionNumber = position,
                        Option = NameLetters[i],
                        Xmm = x,
                        Ymm = NameY + GridFirstCenter + i * NameRowPitch,
                        RadiusMm = GridBubbleRadius
                    });
                }
            }
        }

        private static void AddDigitGrid(
            FormLayout layout, string name, string label,
            int sectionId, double y, int digitCount)
        {
            layout.Fields.Add(new FieldInfo
            {
                Name = name,
                Label = label,
                Type = "DigitGrid",
                Xmm = RightBlockX,
                Ymm = y,
                WidthMm = RightBlockWidth,
                HeightMm = DigitGridHeight,
                CharacterCount = digitCount
            });

            double centerX = RightBlockX + RightBlockWidth / 2;
            double firstX = centerX - (digitCount - 1) * DigitColumnPitch / 2;

            for (int position = 1; position <= digitCount; position++)
            {
                for (int digit = 0; digit <= 9; digit++)
                {
                    layout.Bubbles.Add(new BubbleInfo
                    {
                        SectionId = sectionId,
                        QuestionNumber = position,
                        Option = digit.ToString(CultureInfo.InvariantCulture),
                        Xmm = firstX + (position - 1) * DigitColumnPitch,
                        Ymm = y + GridFirstCenter + digit * DigitRowPitch,
                        RadiusMm = GridBubbleRadius
                    });
                }
            }
        }

        private static void AddInstructions(FormLayout layout)
        {
            layout.Fields.Add(new FieldInfo
            {
                Name = "Instructions",
                Label = "DİKKAT VE SINAV KURALLARI:",
                Type = "Instructions",
                Xmm = RightBlockX,
                Ymm = InstructionsY,
                WidthMm = RightBlockWidth,
                HeightMm = InstructionsHeight
            });
        }

        // Dersleri sütunlara yerleştirir. Sığmazsa null döner.
        private static List<Segment>? TryPlace(
            List<ExamSection> sections, bool newColumnPerSection)
        {
            var result = new List<Segment>();
            int column = 0;
            double y = QuestionsTop;

            foreach (var section in sections)
            {
                if (newColumnPerSection && y > QuestionsTop)
                {
                    column++;
                    y = QuestionsTop;
                }

                int question = 1;

                while (question <= section.QuestionCount)
                {
                    if (column >= MaxColumns)
                    {
                        return null;
                    }

                    int fit = (int)Math.Floor(
                        (QuestionsBottom - ColumnBottomPad
                         - (y + SegmentHeaderHeight)) / RowPitch);

                    if (fit < 1)
                    {
                        if (y <= QuestionsTop)
                        {
                            return null;
                        }

                        column++;
                        y = QuestionsTop;
                        continue;
                    }

                    int take = Math.Min(section.QuestionCount - question + 1, fit);

                    result.Add(new Segment(
                        section, question, question + take - 1, column, y));

                    y += SegmentHeaderHeight + take * RowPitch + SegmentGap;
                    question += take;

                    if (question <= section.QuestionCount)
                    {
                        column++;
                        y = QuestionsTop;
                    }
                }
            }

            return result;
        }

        private static void AddQuestionColumns(FormLayout layout, List<Segment> segments)
        {
            foreach (var group in segments.GroupBy(s => s.Column).OrderBy(g => g.Key))
            {
                double x = PageLeft + group.Key * ColumnStep;

                double bottom = group.Max(s =>
                    s.Y + SegmentHeaderHeight
                    + (s.End - s.Start + 1) * RowPitch) + 0.6;

                layout.Fields.Add(new FieldInfo
                {
                    Name = $"QuestionColumn{group.Key + 1}",
                    Label = string.Empty,
                    Type = "QuestionColumn",
                    Xmm = x,
                    Ymm = QuestionsTop,
                    WidthMm = ColumnWidth,
                    HeightMm = bottom - QuestionsTop
                });

                foreach (var segment in group)
                {
                    int count = segment.End - segment.Start + 1;

                    layout.Sections.Add(new SectionLayout
                    {
                        SectionId = segment.Section.Id,
                        Name = segment.Section.Name,
                        Order = segment.Section.Order,
                        QuestionStart = segment.Start,
                        QuestionEnd = segment.End,
                        TotalQuestionCount = segment.Section.QuestionCount,
                        Xmm = x,
                        Ymm = segment.Y,
                        WidthMm = ColumnWidth,
                        HeightMm = SegmentHeaderHeight + count * RowPitch
                    });

                    for (int i = 0; i < count; i++)
                    {
                        double centerY =
                            segment.Y + SegmentHeaderHeight + (i + 0.5) * RowPitch;

                        for (int option = 0;
                             option < segment.Section.OptionCount;
                             option++)
                        {
                            layout.Bubbles.Add(new BubbleInfo
                            {
                                SectionId = segment.Section.Id,
                                QuestionNumber = segment.Start + i,
                                Option = Options[option].ToString(),
                                Xmm = x + FirstOptionOffset + option * OptionPitch,
                                Ymm = centerY,
                                RadiusMm = QuestionBubbleRadius
                            });
                        }
                    }
                }
            }
        }

        // <PDF-BEGIN>
        // =====================================================================
        // 2) PDF ÜRETİMİ
        // =====================================================================
        public byte[] RenderPdf(FormLayout layout)
        {
            ArgumentNullException.ThrowIfNull(layout);

            if (layout.PageWidthMm <= 0 || layout.PageHeightMm <= 0)
            {
                throw new InvalidOperationException("Sayfa ölçüleri geçersiz.");
            }

            string svg = BuildSvg(layout);

            return Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(
                        (float)layout.PageWidthMm,
                        (float)layout.PageHeightMm,
                        Unit.Millimetre);

                    page.Margin(0);
                    page.Content().Svg(svg).FitArea();
                });
            }).GeneratePdf();
        }
        // <PDF-END>

        // =====================================================================
        // 3) SVG ÇİZİMİ (harita ile birebir aynı koordinatlar)
        // =====================================================================
        public static string BuildSvg(FormLayout layout)
        {
            var svg = new StringBuilder();

            svg.Append(
                $"<svg xmlns=\"http://www.w3.org/2000/svg\" " +
                $"width=\"{F(layout.PageWidthMm)}mm\" " +
                $"height=\"{F(layout.PageHeightMm)}mm\" " +
                $"viewBox=\"0 0 {F(layout.PageWidthMm)} {F(layout.PageHeightMm)}\">");

            AddRect(svg, 0, 0, layout.PageWidthMm, layout.PageHeightMm,
                "white", "none", 0);

            foreach (var marker in layout.Markers)
            {
                AddRect(svg, marker.Xmm, marker.Ymm, marker.SizeMm, marker.SizeMm,
                    "black", "none", 0);
            }

            foreach (var field in layout.Fields)
            {
                switch (field.Type)
                {
                    case "CenteredText":
                        DrawHeaderText(svg, field);
                        break;
                    case "HandwritingBox":
                        DrawHandwritingBox(svg, field);
                        break;
                    case "BookletBox":
                        DrawBookletBox(svg, field);
                        break;
                    case "DigitGrid":
                        DrawGrid(svg, layout, field);
                        break;
                    case "Instructions":
                        DrawInstructions(svg, field);
                        break;
                    case "QuestionColumn":
                        AddRect(svg, field.Xmm, field.Ymm, field.WidthMm,
                            field.HeightMm, "white", "black", 0.35);
                        break;
                }
            }

            // Üst başlık ile soru alanı arasındaki çizgiler.
            AddLine(svg, PageLeft, 27.2, PageLeft + PageWidth, 27.2, "black", 0.5);
            AddLine(svg, PageLeft, SeparatorY, PageLeft + PageWidth, SeparatorY,
                "black", 0.5);

            // Ders başlıkları ve soru numaraları.
            foreach (var section in layout.Sections)
            {
                AddRect(svg, section.Xmm + 0.6, section.Ymm + 0.6,
                    section.WidthMm - 1.2, 4.8, "#f1f5f9", "none", 0);

                AddLine(svg, section.Xmm + 0.6, section.Ymm + 5.4,
                    section.Xmm + section.WidthMm - 0.6, section.Ymm + 5.4,
                    "#94a3b8", 0.2);

                string title = section.QuestionStart == 1
                    ? $"{Shorten(section.Name, 20)} ({section.TotalQuestionCount})"
                    : $"{Shorten(section.Name, 20)} (devam)";

                AddText(svg, section.Xmm + section.WidthMm / 2,
                    section.Ymm + 4.0, title, 2.1, "bold", "middle");

                for (int q = section.QuestionStart; q <= section.QuestionEnd; q++)
                {
                    double centerY = section.Ymm + SegmentHeaderHeight
                        + (q - section.QuestionStart + 0.5) * RowPitch;

                    AddText(svg, section.Xmm + QuestionNumberOffset,
                        centerY + 0.7, q.ToString(CultureInfo.InvariantCulture),
                        2.0, "bold", "end");
                }
            }

            // Bütün daireler (harf/rakam dairenin içinde).
            foreach (var bubble in layout.Bubbles)
            {
                AddCircle(svg, bubble);
            }

            svg.Append("</svg>");
            return svg.ToString();
        }

        private static void DrawHeaderText(StringBuilder svg, FieldInfo field)
        {
            if (string.IsNullOrWhiteSpace(field.Label))
            {
                return;
            }

            double size = field.Name switch
            {
                "InstitutionName" => 4.2,
                "FormTitle" => 3.3,
                _ => 2.0
            };

            string weight = field.Name == "Subtitle" ? "normal" : "bold";
            double baseline = field.Ymm + field.HeightMm - 1.2;

            AddText(svg, field.Xmm + field.WidthMm / 2, baseline,
                field.Label, size, weight, "middle");
        }

        private static void DrawHandwritingBox(StringBuilder svg, FieldInfo f)
        {
            AddRect(svg, f.Xmm, f.Ymm, f.WidthMm, f.HeightMm, "white", "black", 0.4);

            double lineStart = f.Xmm + 24;
            double lineEnd = f.Xmm + f.WidthMm - 2;

            AddText(svg, f.Xmm + 2, f.Ymm + 6.2, "ADI SOYADI:", 2.0, "bold");
            AddLine(svg, lineStart, f.Ymm + 6.6, lineEnd, f.Ymm + 6.6, "black", 0.25);

            AddText(svg, f.Xmm + 2, f.Ymm + 12.2, "SINIF / ŞUBE:", 2.0, "bold");
            AddLine(svg, lineStart, f.Ymm + 12.6, f.Xmm + 62, f.Ymm + 12.6,
                "black", 0.25);

            AddText(svg, f.Xmm + 66, f.Ymm + 12.2, "İMZA:", 2.0, "bold");
            AddLine(svg, f.Xmm + 77, f.Ymm + 12.6, lineEnd, f.Ymm + 12.6,
                "black", 0.25);
        }

        private static void DrawBookletBox(StringBuilder svg, FieldInfo f)
        {
            AddRect(svg, f.Xmm, f.Ymm, f.WidthMm, f.HeightMm, "white", "black", 0.4);
            AddText(svg, f.Xmm + 2, f.Ymm + f.HeightMm / 2 + 0.75,
                f.Label, 2.1, "bold");
        }

        private static void DrawInstructions(StringBuilder svg, FieldInfo f)
        {
            AddRect(svg, f.Xmm, f.Ymm, f.WidthMm, f.HeightMm, "#f8fafc", "black", 0.4);

            AddText(svg, f.Xmm + 2, f.Ymm + 4.2, f.Label, 2.1, "bold");

            string[] lines =
            {
                "1. Cevaplarınızı yumuşak kurşun kalemle, kutucuğu",
                "    taşırmadan doldurunuz.",
                "2. Kitapçık türünü ve T.C. Kimlik numaranızı kodlamayı",
                "    unutmayınız.",
                "3. Yanlış cevaplar doğru cevapları götürmez (Ham puan esası)."
            };

            for (int i = 0; i < lines.Length; i++)
            {
                AddText(svg, f.Xmm + 2, f.Ymm + 8.2 + i * 3.1, lines[i], 1.85);
            }
        }

        private static void DrawGrid(StringBuilder svg, FormLayout layout, FieldInfo f)
        {
            AddRect(svg, f.Xmm, f.Ymm, f.WidthMm, f.HeightMm, "white", "black", 0.4);

            AddText(svg, f.Xmm + f.WidthMm / 2, f.Ymm + 3.9, f.Label,
                2.0, "bold", "middle");

            AddLine(svg, f.Xmm + 2, f.Ymm + 5.2, f.Xmm + f.WidthMm - 2,
                f.Ymm + 5.2, "black", 0.25);

            int sectionId = f.Name switch
            {
                "NameGrid" => NameSectionId,
                "TcNumber" => TcNumberSectionId,
                _ => StudentNumberSectionId
            };

            string firstOption = sectionId == NameSectionId ? "A" : "0";

            // Her karakter sütununun üstüne yazı kutusu çiz.
            foreach (var bubble in layout.Bubbles.Where(b =>
                         b.SectionId == sectionId && b.Option == firstOption))
            {
                AddRect(svg, bubble.Xmm - GridBoxSize / 2, f.Ymm + GridBoxTop,
                    GridBoxSize, GridBoxSize, "#f8fafc", "black", 0.22);
            }
        }

        private static string Shorten(string value, int max)
        {
            return value.Length <= max ? value : value.Substring(0, max - 1) + "…";
        }

        private static void AddRect(
            StringBuilder svg, double x, double y, double w, double h,
            string fill, string stroke, double strokeWidth)
        {
            svg.Append(
                $"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(w)}\" height=\"{F(h)}\" " +
                $"fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"{F(strokeWidth)}\"/>");
        }

        private static void AddLine(
            StringBuilder svg, double x1, double y1, double x2, double y2,
            string stroke, double strokeWidth)
        {
            svg.Append(
                $"<line x1=\"{F(x1)}\" y1=\"{F(y1)}\" x2=\"{F(x2)}\" y2=\"{F(y2)}\" " +
                $"stroke=\"{stroke}\" stroke-width=\"{F(strokeWidth)}\"/>");
        }

        private static void AddCircle(StringBuilder svg, BubbleInfo bubble)
        {
            svg.Append(
                $"<circle cx=\"{F(bubble.Xmm)}\" cy=\"{F(bubble.Ymm)}\" " +
                $"r=\"{F(bubble.RadiusMm)}\" fill=\"white\" stroke=\"black\" " +
                $"stroke-width=\"0.22\"/>");

            double fontSize = bubble.RadiusMm * 1.3;

            AddText(svg, bubble.Xmm, bubble.Ymm + fontSize * 0.36,
                bubble.Option, fontSize, "bold", "middle");
        }

        private static void AddText(
            StringBuilder svg, double x, double y, string value,
            double fontSize, string weight = "normal", string anchor = "start")
        {
            string safeText = SecurityElement.Escape(value) ?? string.Empty;

            svg.Append(
                $"<text x=\"{F(x)}\" y=\"{F(y)}\" " +
                $"font-family=\"Arial, sans-serif\" " +
                $"font-size=\"{F(fontSize)}\" font-weight=\"{weight}\" " +
                $"text-anchor=\"{anchor}\" xml:space=\"preserve\">{safeText}</text>");
        }

        private static string F(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
