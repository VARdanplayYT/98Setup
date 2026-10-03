#nullable enable
// by vardanplay
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows.Forms;

namespace _98Setup
{
    public partial class Form1 : Form
    {
        private Rectangle closeButtonRect;
        private bool isDraggingForm;
        private Point dragCursorPoint;
        private Point dragFormPoint;

        private RetroTextBoxPanel txtFolder = null!;
        private RetroTextBoxPanel txtTargetDir = null!;
        private RetroTextBoxPanel txtTitle = null!;
        private RetroTextBoxPanel txtAppName = null!;
        private RetroTextBoxPanel txtMessage = null!;
        private RetroColorButton btnPickColor = null!;
        private Color currentTitleColor = Color.FromArgb(0, 0, 128);

        private byte[]? customWindowImageBytes = null;
        private byte[]? appExeIconBytes = null;
        private bool isWindowIconVisible = true;
        private Label lblAppIconStatus = null!;

        private Panel pnlCanvasContainer = null!;
        private Panel pnlCanvas = null!;
        private Label lblWinSizeInfo = null!;
        private Label lblSelectedInfo = null!;

        private Label canvasLblPath = null!;
        private RetroTextBoxPanel canvasTxtPath = null!;
        private RetroWin98Button canvasBtnBrowse = null!;
        private PictureBox canvasPicIcon = null!;
        private Label canvasLblAppName = null!;
        private Label canvasLblMessage = null!;
        private RetroWin98Button canvasBtnExit = null!;
        private RetroWin98Button canvasBtnInstall = null!;

        private Control? selectedEditorControl = null;
        private Point dragStartPoint;
        private bool isDraggingControl = false;

        public Form1()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(910, 590);
            DoubleBuffered = true;
            BackColor = Color.FromArgb(192, 192, 192);

            BuildStudioUI();
        }

        private void BuildStudioUI()
        {
            int left = 20;
            int y = 35;
            int leftWidth = 280;

            AddLabel("Папка с файлами приложения:", left, y);
            txtFolder = new RetroTextBoxPanel(@"C:\") { Left = left, Top = y + 18, Width = leftWidth - 80 };
            var btnBrowseFolder = new RetroWin98Button { Text = "Обзор...", Left = left + leftWidth - 75, Top = y + 17, Width = 75, Height = 24 };
            btnBrowseFolder.Click += (s, e) =>
            {
                using var fbd = new FolderBrowserDialog();
                if (fbd.ShowDialog() == DialogResult.OK) txtFolder.TextValue = fbd.SelectedPath;
            };
            Controls.AddRange(new Control[] { txtFolder, btnBrowseFolder });
            y += 46;

            AddLabel("Папка установки по умолчанию:", left, y);
            txtTargetDir = new RetroTextBoxPanel(@"C:\Games\MyRetroApp") { Left = left, Top = y + 18, Width = leftWidth };
            txtTargetDir.InnerBox.TextChanged += (s, e) => { if (canvasTxtPath != null) canvasTxtPath.TextValue = txtTargetDir.TextValue; };
            Controls.Add(txtTargetDir);
            y += 46;

            AddLabel("Заголовок окна (Title):", left, y);
            txtTitle = new RetroTextBoxPanel("System message") { Left = left, Top = y + 18, Width = leftWidth };
            txtTitle.InnerBox.TextChanged += (s, e) => pnlCanvas.Invalidate();
            Controls.Add(txtTitle);
            y += 46;

            AddLabel("Название программы:", left, y);
            txtAppName = new RetroTextBoxPanel("My Retro Game") { Left = left, Top = y + 18, Width = leftWidth };
            txtAppName.InnerBox.TextChanged += (s, e) => { if (canvasLblAppName != null) canvasLblAppName.Text = txtAppName.TextValue; };
            Controls.Add(txtAppName);
            y += 46;

            AddLabel("Описание / Текст сообщения:", left, y);
            txtMessage = new RetroTextBoxPanel("Critical error") { Left = left, Top = y + 18, Width = leftWidth };
            txtMessage.InnerBox.TextChanged += (s, e) => { if (canvasLblMessage != null) canvasLblMessage.Text = txtMessage.TextValue; };
            Controls.Add(txtMessage);
            y += 46;

            btnPickColor = new RetroColorButton { Text = "Цвет шапки", Left = left, Top = y, Width = leftWidth, Height = 25, SelectedColor = currentTitleColor };
            btnPickColor.Click += (s, e) =>
            {
                using var cd = new ColorDialog { Color = currentTitleColor };
                if (cd.ShowDialog() == DialogResult.OK)
                {
                    currentTitleColor = cd.Color;
                    btnPickColor.SelectedColor = currentTitleColor;
                    pnlCanvas.Invalidate();
                    Invalidate();
                }
            };
            Controls.Add(btnPickColor);
            y += 30;

            AddLabel("Картинка внутри окна инсталлятора:", left, y, isBold: true);
            y += 18;

            var btnLoadWindowImg = new RetroWin98Button { Text = "Картинка в окно...", Left = left, Top = y, Width = 135, Height = 24 };
            btnLoadWindowImg.Click += BtnLoadWindowImg_Click;

            var btnDeleteWindowImg = new RetroWin98Button { Text = "Удалить иконку", Left = left + 140, Top = y, Width = 140, Height = 24 };
            btnDeleteWindowImg.Click += (s, e) =>
            {
                isWindowIconVisible = false;
                canvasPicIcon.Visible = false;
                if (selectedEditorControl == canvasPicIcon) selectedEditorControl = null;
                pnlCanvas.Invalidate();
            };

            Controls.AddRange(new Control[] { btnLoadWindowImg, btnDeleteWindowImg });
            y += 26;

            var btnResetWindowImg = new RetroWin98Button { Text = "Стандартная иконка ошибки", Left = left, Top = y, Width = leftWidth, Height = 24 };
            btnResetWindowImg.Click += (s, e) =>
            {
                customWindowImageBytes = null;
                isWindowIconVisible = true;
                canvasPicIcon.Visible = true;
                UpdateCanvasIcon();
                pnlCanvas.Invalidate();
            };
            Controls.Add(btnResetWindowImg);
            y += 32;

            AddLabel("Иконка приложения (EXE и панель задач):", left, y, isBold: true);
            y += 18;

            var btnLoadAppIcon = new RetroWin98Button { Text = "Выбрать иконку EXE...", Left = left, Top = y, Width = 160, Height = 24 };
            btnLoadAppIcon.Click += BtnLoadAppIcon_Click;

            var btnResetAppIcon = new RetroWin98Button { Text = "Сброс", Left = left + 165, Top = y, Width = 115, Height = 24 };
            btnResetAppIcon.Click += (s, e) =>
            {
                appExeIconBytes = null;
                lblAppIconStatus.Text = "Иконка EXE: стандартная";
            };
            Controls.AddRange(new Control[] { btnLoadAppIcon, btnResetAppIcon });
            y += 25;

            lblAppIconStatus = new Label { Text = "Иконка EXE: стандартная", Left = left, Top = y, AutoSize = true, Font = new Font("Tahoma", 7.5f) };
            Controls.Add(lblAppIconStatus);
            y += 24;

            var btnBuild = new RetroWin98Button
            {
                Text = "Собрать Setup.exe",
                Left = left,
                Top = y,
                Width = leftWidth,
                Height = 44,
                Font = new Font("Tahoma", 9f, FontStyle.Bold)
            };
            btnBuild.Click += BtnBuild_Click;
            Controls.Add(btnBuild);

            int rightLeft = 320;
            int rightY = 35;

            AddLabel("Редактор интерфейса Setup (передвигайте элементы мышкой):", rightLeft, rightY, isBold: true);
            rightY += 22;

            lblWinSizeInfo = new Label { Text = "Размер окна: 460 x 280", Left = rightLeft, Top = rightY + 3, AutoSize = true, Font = new Font("Tahoma", 8.25f, FontStyle.Bold) };
            var btnWinWMinus = new RetroWin98Button { Text = "W -", Left = rightLeft + 240, Top = rightY, Width = 45, Height = 22 };
            var btnWinWPlus = new RetroWin98Button { Text = "W +", Left = rightLeft + 290, Top = rightY, Width = 45, Height = 22 };
            var btnWinHMinus = new RetroWin98Button { Text = "H -", Left = rightLeft + 345, Top = rightY, Width = 45, Height = 22 };
            var btnWinHPlus = new RetroWin98Button { Text = "H +", Left = rightLeft + 395, Top = rightY, Width = 45, Height = 22 };

            btnWinWMinus.Click += (s, e) => AdjustWindowSize(-20, 0);
            btnWinWPlus.Click += (s, e) => AdjustWindowSize(20, 0);
            btnWinHMinus.Click += (s, e) => AdjustWindowSize(0, -20);
            btnWinHPlus.Click += (s, e) => AdjustWindowSize(0, 20);

            Controls.AddRange(new Control[] { lblWinSizeInfo, btnWinWMinus, btnWinWPlus, btnWinHMinus, btnWinHPlus });
            rightY += 26;

            lblSelectedInfo = new Label { Text = "Выбран: (кликните по элементу)", Left = rightLeft, Top = rightY + 3, AutoSize = true, Font = new Font("Tahoma", 8.25f) };
            var btnElemWMinus = new RetroWin98Button { Text = "W -", Left = rightLeft + 240, Top = rightY, Width = 45, Height = 22 };
            var btnElemWPlus = new RetroWin98Button { Text = "W +", Left = rightLeft + 290, Top = rightY, Width = 45, Height = 22 };
            var btnElemHMinus = new RetroWin98Button { Text = "H -", Left = rightLeft + 345, Top = rightY, Width = 45, Height = 22 };
            var btnElemHPlus = new RetroWin98Button { Text = "H +", Left = rightLeft + 395, Top = rightY, Width = 45, Height = 22 };

            btnElemWMinus.Click += (s, e) => AdjustElementSize(-5, 0);
            btnElemWPlus.Click += (s, e) => AdjustElementSize(5, 0);
            btnElemHMinus.Click += (s, e) => AdjustElementSize(0, -5);
            btnElemHPlus.Click += (s, e) => AdjustElementSize(0, 5);

            Controls.AddRange(new Control[] { lblSelectedInfo, btnElemWMinus, btnElemWPlus, btnElemHMinus, btnElemHPlus });
            rightY += 28;

            pnlCanvasContainer = new Panel
            {
                Left = rightLeft,
                Top = rightY,
                Width = 560,
                Height = 460,
                AutoScroll = true,
                BackColor = Color.FromArgb(160, 160, 160)
            };
            Controls.Add(pnlCanvasContainer);

            pnlCanvas = new Panel
            {
                Left = 10,
                Top = 10,
                Width = 460,
                Height = 280,
                BackColor = Color.FromArgb(192, 192, 192)
            };
            pnlCanvas.Paint += PnlCanvas_Paint;
            pnlCanvasContainer.Controls.Add(pnlCanvas);

            canvasLblPath = new Label { Text = "Папка установки:", Left = 20, Top = 35, AutoSize = true, Font = new Font("Tahoma", 8.25f) };
            canvasTxtPath = new RetroTextBoxPanel(txtTargetDir.TextValue) { Left = 20, Top = 55, Width = 310, Height = 24 };
            canvasBtnBrowse = new RetroWin98Button { Text = "Обзор...", Left = 340, Top = 54, Width = 95, Height = 24 };

            canvasPicIcon = new PictureBox
            {
                Left = 25,
                Top = 100,
                Width = 44,
                Height = 44,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            UpdateCanvasIcon();

            canvasLblAppName = new Label
            {
                Text = txtAppName.TextValue,
                Left = 85,
                Top = 100,
                Width = 350,
                Height = 20,
                Font = new Font("Tahoma", 9f, FontStyle.Bold)
            };

            canvasLblMessage = new Label
            {
                Text = txtMessage.TextValue,
                Left = 85,
                Top = 125,
                Width = 350,
                Height = 40,
                Font = new Font("Courier New", 10.5f, FontStyle.Bold)
            };

            canvasBtnExit = new RetroWin98Button { Text = "Выйти", Left = 20, Top = 235, Width = 95, Height = 26 };
            canvasBtnInstall = new RetroWin98Button { Text = "Установить", Left = 340, Top = 235, Width = 95, Height = 26, Font = new Font("Tahoma", 8.25f, FontStyle.Bold) };

            MakeDraggable(canvasLblPath, "Метка пути");
            MakeDraggable(canvasTxtPath, "Поле пути");
            MakeDraggable(canvasBtnBrowse, "Кнопка Обзор");
            MakeDraggable(canvasPicIcon, "Иконка");
            MakeDraggable(canvasLblAppName, "Имя программы");
            MakeDraggable(canvasLblMessage, "Текст сообщения");
            MakeDraggable(canvasBtnExit, "Кнопка Выйти");
            MakeDraggable(canvasBtnInstall, "Кнопка Установить");

            pnlCanvas.Controls.AddRange(new Control[] {
                canvasLblPath, canvasTxtPath, canvasBtnBrowse,
                canvasPicIcon, canvasLblAppName, canvasLblMessage,
                canvasBtnExit, canvasBtnInstall
            });
        }

        private void AdjustWindowSize(int deltaW, int deltaH)
        {
            int newW = Math.Max(320, pnlCanvas.Width + deltaW);
            int newH = Math.Max(200, pnlCanvas.Height + deltaH);

            pnlCanvas.Size = new Size(newW, newH);
            lblWinSizeInfo.Text = $"Размер окна: {newW} x {newH}";

            foreach (Control c in pnlCanvas.Controls)
            {
                if (c.Right > newW - 6) c.Left = Math.Max(6, newW - c.Width - 6);
                if (c.Bottom > newH - 6) c.Top = Math.Max(26, newH - c.Height - 6);
            }

            pnlCanvas.Invalidate();
        }

        private void AdjustElementSize(int deltaW, int deltaH)
        {
            if (selectedEditorControl == null) return;
            selectedEditorControl.Width = Math.Max(15, selectedEditorControl.Width + deltaW);
            selectedEditorControl.Height = Math.Max(15, selectedEditorControl.Height + deltaH);
            lblSelectedInfo.Text = $"Выбран: {selectedEditorControl.Tag} ({selectedEditorControl.Width}x{selectedEditorControl.Height})";
            pnlCanvas.Invalidate();
        }

        private void MakeDraggable(Control c, string name)
        {
            c.Tag = name;
            c.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    selectedEditorControl = c;
                    dragStartPoint = e.Location;
                    isDraggingControl = true;
                    lblSelectedInfo.Text = $"Выбран: {name} ({c.Width}x{c.Height})";
                    pnlCanvas.Invalidate();
                }
            };
            c.MouseMove += (s, e) =>
            {
                if (isDraggingControl && selectedEditorControl == c)
                {
                    int newX = c.Left + e.X - dragStartPoint.X;
                    int newY = c.Top + e.Y - dragStartPoint.Y;

                    newX = Math.Max(6, Math.Min(newX, pnlCanvas.Width - c.Width - 6));
                    newY = Math.Max(26, Math.Min(newY, pnlCanvas.Height - c.Height - 6));

                    c.Left = newX;
                    c.Top = newY;
                    pnlCanvas.Invalidate();
                }
            };
            c.MouseUp += (s, e) => isDraggingControl = false;
        }

        private void PnlCanvas_Paint(object? sender, PaintEventArgs e)
        {
            Rectangle dummyClose;
            RetroPaintEngine.DrawWindowFrame(e.Graphics, pnlCanvas.ClientRectangle, txtTitle.TextValue, currentTitleColor, out dummyClose);

            ControlPaint.DrawBorder3D(e.Graphics, new Rectangle(10, pnlCanvas.Height - 55, pnlCanvas.Width - 20, 2), Border3DStyle.Etched);

            if (selectedEditorControl != null && selectedEditorControl.Visible && selectedEditorControl.Parent == pnlCanvas)
            {
                Rectangle selRect = selectedEditorControl.Bounds;
                selRect.Inflate(2, 2);
                using var p = new Pen(Color.Navy, 1f) { DashStyle = DashStyle.Dot };
                e.Graphics.DrawRectangle(p, selRect);
            }
        }

        private void BtnLoadWindowImg_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Выберите картинку для окна установщика",
                Filter = "Все изображения (*.png;*.jpg;*.bmp;*.ico)|*.png;*.jpg;*.jpeg;*.bmp;*.ico;*.gif"
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    byte[] fileBytes = File.ReadAllBytes(ofd.FileName);
                    using var testImg = RetroImageHelper.CreateDetachedBitmap(fileBytes);

                    customWindowImageBytes = fileBytes;
                    isWindowIconVisible = true;
                    canvasPicIcon.Visible = true;
                    UpdateCanvasIcon();
                    pnlCanvas.Invalidate();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Не удалось прочитать выбранную картинку:\n" + ex.Message, "Ошибка");
                }
            }
        }

        private void BtnLoadAppIcon_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Выберите иконку приложения (.ico или .png)",
                Filter = "Иконки и изображения (*.ico;*.png;*.jpg;*.bmp)|*.ico;*.png;*.jpg;*.jpeg;*.bmp"
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    byte[] fileBytes = File.ReadAllBytes(ofd.FileName);
                    appExeIconBytes = RetroImageHelper.ConvertToIco(fileBytes);
                    lblAppIconStatus.Text = "Иконка EXE: " + Path.GetFileName(ofd.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка конвертации иконки:\n" + ex.Message, "Ошибка");
                }
            }
        }

        private void UpdateCanvasIcon()
        {
            if (canvasPicIcon == null) return;

            var oldImg = canvasPicIcon.Image;
            canvasPicIcon.Image = null;
            oldImg?.Dispose();

            if (!isWindowIconVisible) return;

            if (customWindowImageBytes != null && customWindowImageBytes.Length > 0)
            {
                try
                {
                    canvasPicIcon.Image = RetroImageHelper.CreateDetachedBitmap(customWindowImageBytes);
                    return;
                }
                catch
                {
                    customWindowImageBytes = null;
                }
            }

            Bitmap bmp = new Bitmap(44, 44);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var b = new SolidBrush(Color.FromArgb(204, 0, 0));
                g.FillEllipse(b, 0, 0, 43, 43);
                using var pen = new Pen(Color.White, 4f) { StartCap = LineCap.Square, EndCap = LineCap.Square };
                g.DrawLine(pen, 12, 12, 31, 31);
                g.DrawLine(pen, 31, 12, 12, 31);
            }
            canvasPicIcon.Image = bmp;
        }

        private SetupConfig GetCurrentConfig() => new SetupConfig
        {
            Title = txtTitle.TextValue,
            Message = txtMessage.TextValue,
            AppName = txtAppName.TextValue,
            DefaultInstallDir = txtTargetDir.TextValue,
            TitleColorArgb = currentTitleColor.ToArgb(),
            WindowWidth = pnlCanvas.Width,
            WindowHeight = pnlCanvas.Height,
            ShowWindowIcon = isWindowIconVisible,
            HasCustomAppIcon = appExeIconBytes != null,

            LblPathBounds = canvasLblPath.Bounds,
            TxtPathBounds = canvasTxtPath.Bounds,
            BtnBrowseBounds = canvasBtnBrowse.Bounds,
            PicIconBounds = canvasPicIcon.Bounds,
            LblAppNameBounds = canvasLblAppName.Bounds,
            LblMessageBounds = canvasLblMessage.Bounds,
            BtnExitBounds = canvasBtnExit.Bounds,
            BtnInstallBounds = canvasBtnInstall.Bounds
        };

        private void BtnBuild_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFolder.TextValue) || !Directory.Exists(txtFolder.TextValue))
            {
                MessageBox.Show("Укажите существующую папку с файлами для сборки!", "98Setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var sfd = new SaveFileDialog { Filter = "Исполняемый файл (*.exe)|*.exe", FileName = "Setup.exe" };
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    StandaloneCompiler.Build(txtFolder.TextValue, sfd.FileName, GetCurrentConfig(), customWindowImageBytes, appExeIconBytes);
                    MessageBox.Show(
                        $"Установщик успешно собран!\n\nФайл: {sfd.FileName}\n\n" +
                        (appExeIconBytes != null ? "Иконка установщика обновлена для панели задач и Проводника.\n" : "") +
                        (!isWindowIconVisible ? "Иконка внутри окна отключена." : "Иконка внутри окна установлена."),
                        "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка сборки: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            RetroPaintEngine.DrawWindowFrame(e.Graphics, ClientRectangle, "98Setup Studio - Конструктор Установщиков", currentTitleColor, out closeButtonRect);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (closeButtonRect.Contains(e.Location)) Close();
            else if (e.Y <= 24 && e.Button == MouseButtons.Left)
            {
                isDraggingForm = true;
                dragCursorPoint = Cursor.Position;
                dragFormPoint = Location;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (isDraggingForm)
            {
                Point diff = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
                Location = Point.Add(dragFormPoint, new Size(diff));
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            isDraggingForm = false;
        }

        private void AddLabel(string text, int x, int y, bool isBold = false) =>
            Controls.Add(new Label { Text = text, Left = x, Top = y, AutoSize = true, Font = new Font("Tahoma", 8.25f, isBold ? FontStyle.Bold : FontStyle.Regular) });

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }

    public static class RetroImageHelper
    {
        public static Bitmap CreateDetachedBitmap(byte[] data)
        {
            using var ms = new MemoryStream(data);
            try
            {
                using var raw = Image.FromStream(ms);
                return new Bitmap(raw);
            }
            catch
            {
                ms.Position = 0;
                using var ico = new Icon(ms);
                return ico.ToBitmap();
            }
        }

        public static byte[] ConvertToIco(byte[] inputBytes)
        {
            if (inputBytes.Length > 4 && inputBytes[0] == 0 && inputBytes[1] == 0 && inputBytes[2] == 1 && inputBytes[3] == 0)
            {
                return inputBytes;
            }

            using var srcBmp = CreateDetachedBitmap(inputBytes);
            using var resized = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.Clear(Color.Transparent);
                g.DrawImage(srcBmp, 0, 0, 32, 32);
            }

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write((short)0);
            bw.Write((short)1);
            bw.Write((short)1);

            int width = 32;
            int height = 32;
            int maskRowBytes = ((width + 31) / 32) * 4;
            int maskSize = maskRowBytes * height;
            int pixelDataSize = width * height * 4;
            int biSizeImage = 40 + pixelDataSize + maskSize;

            bw.Write((byte)width);
            bw.Write((byte)height);
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((short)1);
            bw.Write((short)32);
            bw.Write((int)biSizeImage);
            bw.Write((int)22);

            bw.Write((int)40);
            bw.Write((int)width);
            bw.Write((int)(height * 2));
            bw.Write((short)1);
            bw.Write((short)32);
            bw.Write((int)0);
            bw.Write((int)(pixelDataSize + maskSize));
            bw.Write((int)0);
            bw.Write((int)0);
            bw.Write((int)0);
            bw.Write((int)0);

            for (int y = height - 1; y >= 0; y--)
            {
                for (int x = 0; x < width; x++)
                {
                    Color c = resized.GetPixel(x, y);
                    bw.Write(c.B);
                    bw.Write(c.G);
                    bw.Write(c.R);
                    bw.Write(c.A);
                }
            }

            for (int y = height - 1; y >= 0; y--)
            {
                byte maskByte = 0;
                int bitPos = 7;
                for (int x = 0; x < width; x++)
                {
                    Color c = resized.GetPixel(x, y);
                    if (c.A < 128) maskByte |= (byte)(1 << bitPos);
                    bitPos--;
                    if (bitPos < 0)
                    {
                        bw.Write(maskByte);
                        maskByte = 0;
                        bitPos = 7;
                    }
                }
            }

            bw.Flush();
            return ms.ToArray();
        }
    }

    public class SetupConfig
    {
        public string Title { get; set; } = "System message";
        public string Message { get; set; } = "Critical error";
        public string AppName { get; set; } = "My Retro Game";
        public string DefaultInstallDir { get; set; } = @"C:\Games\MyRetroApp";
        public int TitleColorArgb { get; set; } = Color.FromArgb(0, 0, 128).ToArgb();

        public int WindowWidth { get; set; } = 460;
        public int WindowHeight { get; set; } = 280;

        public bool ShowWindowIcon { get; set; } = true;
        public bool HasCustomAppIcon { get; set; } = false;

        public Rectangle LblPathBounds { get; set; }
        public Rectangle TxtPathBounds { get; set; }
        public Rectangle BtnBrowseBounds { get; set; }
        public Rectangle PicIconBounds { get; set; }
        public Rectangle LblAppNameBounds { get; set; }
        public Rectangle LblMessageBounds { get; set; }
        public Rectangle BtnExitBounds { get; set; }
        public Rectangle BtnInstallBounds { get; set; }
    }

    public class RetroTextBoxPanel : Panel
    {
        public TextBox InnerBox { get; }

        public RetroTextBoxPanel(string defaultText = "")
        {
            BackColor = Color.White;
            Padding = new Padding(3, 4, 3, 3);
            Height = 24;

            InnerBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                Text = defaultText,
                Font = new Font("Tahoma", 8.25f),
                BackColor = Color.White,
                ForeColor = Color.Black
            };
            Controls.Add(InnerBox);
        }

        public string TextValue
        {
            get => InnerBox.Text;
            set => InnerBox.Text = value;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            ControlPaint.DrawBorder3D(e.Graphics, ClientRectangle, Border3DStyle.Sunken);
        }
    }

    public class RetroColorButton : RetroWin98Button
    {
        private Color _selectedColor = Color.FromArgb(0, 0, 128);

        public Color SelectedColor
        {
            get => _selectedColor;
            set { _selectedColor = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            base.OnPaint(pevent);
            int size = 12;
            Rectangle swatch = new Rectangle(Width - size - 8, (Height - size) / 2, size, size);
            using (var b = new SolidBrush(_selectedColor))
                pevent.Graphics.FillRectangle(b, swatch);
            ControlPaint.DrawBorder3D(pevent.Graphics, swatch, Border3DStyle.Sunken);
        }
    }

    public class RetroWin98Button : Button
    {
        public RetroWin98Button()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = new Font("Tahoma", 8.25f);
            BackColor = Color.FromArgb(192, 192, 192);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            base.OnPaint(pevent);
            Graphics g = pevent.Graphics;
            Rectangle rect = ClientRectangle;

            bool isPressed = (MouseButtons & MouseButtons.Left) != 0 && rect.Contains(PointToClient(MousePosition));

            using (var brush = new SolidBrush(BackColor))
                g.FillRectangle(brush, rect);

            ControlPaint.DrawBorder3D(g, rect, isPressed ? Border3DStyle.Sunken : Border3DStyle.Raised);

            Rectangle textRect = rect;
            if (isPressed) textRect.Offset(1, 1);

            TextRenderer.DrawText(g, Text, Font, textRect, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    public static class RetroPaintEngine
    {
        public static void DrawWindowFrame(Graphics g, Rectangle bounds, string title, Color titleColor, out Rectangle closeRect)
        {
            g.SmoothingMode = SmoothingMode.None;

            ControlPaint.DrawBorder3D(g, bounds, Border3DStyle.Raised);

            Rectangle titleRect = new Rectangle(3, 3, bounds.Width - 6, 20);
            using (var brush = new SolidBrush(titleColor))
                g.FillRectangle(brush, titleRect);

            using (var font = new Font("Tahoma", 8.25f, FontStyle.Bold))
                TextRenderer.DrawText(g, title, font, new Point(6, 6), Color.White);

            closeRect = new Rectangle(bounds.Width - 21, 5, 16, 15);

            using (var redBrush = new SolidBrush(Color.FromArgb(196, 43, 28)))
                g.FillRectangle(redBrush, closeRect);

            ControlPaint.DrawBorder3D(g, closeRect, Border3DStyle.Raised);

            using (var pen = new Pen(Color.White, 2f))
            {
                g.DrawLine(pen, closeRect.X + 4, closeRect.Y + 3, closeRect.Right - 5, closeRect.Bottom - 4);
                g.DrawLine(pen, closeRect.Right - 5, closeRect.Y + 3, closeRect.X + 4, closeRect.Bottom - 4);
            }
        }
    }

    public static class StandaloneCompiler
    {
        public static void Build(string sourceFolder, string outputExePath, SetupConfig config, byte[]? customWindowImg, byte[]? appIcon)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "98Setup_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string tempZip = Path.Combine(tempDir, "payload.zip");
            string tempCs = Path.Combine(tempDir, "Installer.cs");
            string tempWinImg = Path.Combine(tempDir, "win_icon.dat");
            string tempAppIcon = Path.Combine(tempDir, "app_icon.ico");

            try
            {
                ZipFile.CreateFromDirectory(sourceFolder, tempZip, CompressionLevel.Optimal, false);

                string resourceArgs = $"/resource:\"{tempZip}\",payload.zip";

                if (config.ShowWindowIcon && customWindowImg != null && customWindowImg.Length > 0)
                {
                    File.WriteAllBytes(tempWinImg, customWindowImg);
                    resourceArgs += $" /resource:\"{tempWinImg}\",window_icon";
                }

                string win32IconArg = "";
                if (appIcon != null && appIcon.Length > 0)
                {
                    File.WriteAllBytes(tempAppIcon, appIcon);
                    win32IconArg = $"/win32icon:\"{tempAppIcon}\" ";
                    resourceArgs += $" /resource:\"{tempAppIcon}\",taskbar_icon";
                }

                string sourceCode = GenerateInstallerSource(config, customWindowImg != null);
                File.WriteAllText(tempCs, sourceCode, Encoding.UTF8);

                string? cscPath = FindCscPath();
                if (cscPath == null) throw new Exception("Не найден csc.exe в папке Windows!");

                var psi = new ProcessStartInfo
                {
                    FileName = cscPath,
                    Arguments = $"/target:winexe /nologo /optimize+ /platform:anycpu {win32IconArg}" +
                                $"/reference:System.dll,System.Windows.Forms.dll,System.Drawing.dll,System.IO.Compression.FileSystem.dll " +
                                $"{resourceArgs} /out:\"{outputExePath}\" \"{tempCs}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = Process.Start(psi) ?? throw new Exception("Не удалось запустить компилятор");
                string output = proc.StandardOutput.ReadToEnd();
                string error = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                if (proc.ExitCode != 0)
                    throw new Exception($"Ошибка компиляции:\n{output}\n{error}");
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        private static string? FindCscPath()
        {
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string[] paths = {
                Path.Combine(win, @"Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
                Path.Combine(win, @"Microsoft.NET\Framework\v4.0.30319\csc.exe")
            };
            foreach (var p in paths) if (File.Exists(p)) return p;
            return null;
        }

        private static string GenerateInstallerSource(SetupConfig cfg, bool hasCustomWindowImg)
        {
            return $@"using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Windows.Forms;

public class Program
{{
    [STAThread]
    public static void Main()
    {{
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new InstallerForm());
    }}
}}

public class InstallerForm : Form
{{
    private Rectangle closeButtonRect;
    private bool isDragging;
    private Point dragCursorPoint;
    private Point dragFormPoint;

    private TextBox txtPath;
    private Button btnInstall;
    private Button btnExit;
    private PictureBox picIcon;

    public InstallerForm()
    {{
        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Size = new Size({cfg.WindowWidth}, {cfg.WindowHeight});
        this.DoubleBuffered = true;
        this.BackColor = Color.FromArgb(192, 192, 192);
        this.ShowInTaskbar = true;

        {(cfg.HasCustomAppIcon ? @"
        try {
            using (Stream s = typeof(Program).Assembly.GetManifestResourceStream(""taskbar_icon"")) {
                if (s != null) this.Icon = new Icon(s);
            }
        } catch { }
        " : "")}

        Label lblPath = new Label {{ 
            Text = ""Папка установки:"", 
            Left = {cfg.LblPathBounds.X}, Top = {cfg.LblPathBounds.Y}, 
            Width = {cfg.LblPathBounds.Width}, Height = {cfg.LblPathBounds.Height}, 
            AutoSize = true, Font = new Font(""Tahoma"", 8.25f) 
        }};
        
        Panel pnlBox = new Panel {{ 
            Left = {cfg.TxtPathBounds.X}, Top = {cfg.TxtPathBounds.Y}, 
            Width = {cfg.TxtPathBounds.Width}, Height = {cfg.TxtPathBounds.Height}, 
            BackColor = Color.White, Padding = new Padding(3, 4, 3, 3) 
        }};
        pnlBox.Paint += (s, e) => ControlPaint.DrawBorder3D(e.Graphics, pnlBox.ClientRectangle, Border3DStyle.Sunken);
        txtPath = new TextBox {{ 
            BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, 
            Text = @""{cfg.DefaultInstallDir.Replace("\\", "\\\\")}"", 
            Font = new Font(""Tahoma"", 8.25f) 
        }};
        pnlBox.Controls.Add(txtPath);

        Button btnBrowse = CreateButton(""Обзор..."", {cfg.BtnBrowseBounds.X}, {cfg.BtnBrowseBounds.Y}, {cfg.BtnBrowseBounds.Width}, {cfg.BtnBrowseBounds.Height});
        btnBrowse.Click += (s, e) => {{
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                if (fbd.ShowDialog() == DialogResult.OK) txtPath.Text = fbd.SelectedPath;
        }};

        picIcon = new PictureBox {{ 
            Left = {cfg.PicIconBounds.X}, Top = {cfg.PicIconBounds.Y}, 
            Width = {cfg.PicIconBounds.Width}, Height = {cfg.PicIconBounds.Height}, 
            SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent,
            Visible = {(cfg.ShowWindowIcon ? "true" : "false")}
        }};
        LoadWindowIcon();

        Label lblAppName = new Label {{ 
            Text = @""{cfg.AppName}"", 
            Left = {cfg.LblAppNameBounds.X}, Top = {cfg.LblAppNameBounds.Y}, 
            Width = {cfg.LblAppNameBounds.Width}, Height = {cfg.LblAppNameBounds.Height}, 
            Font = new Font(""Tahoma"", 9f, FontStyle.Bold) 
        }};

        Label lblMsg = new Label {{ 
            Text = @""{cfg.Message}"", 
            Left = {cfg.LblMessageBounds.X}, Top = {cfg.LblMessageBounds.Y}, 
            Width = {cfg.LblMessageBounds.Width}, Height = {cfg.LblMessageBounds.Height}, 
            Font = new Font(""Courier New"", 10.5f, FontStyle.Bold) 
        }};

        Panel pnlDivider = new Panel {{ Left = 10, Top = {cfg.WindowHeight - 55}, Width = {cfg.WindowWidth - 20}, Height = 2 }};
        pnlDivider.Paint += (s, e) => ControlPaint.DrawBorder3D(e.Graphics, pnlDivider.ClientRectangle, Border3DStyle.Etched);

        btnExit = CreateButton(""Выйти"", {cfg.BtnExitBounds.X}, {cfg.BtnExitBounds.Y}, {cfg.BtnExitBounds.Width}, {cfg.BtnExitBounds.Height});
        btnExit.Click += (s, e) => Close();

        btnInstall = CreateButton(""Установить"", {cfg.BtnInstallBounds.X}, {cfg.BtnInstallBounds.Y}, {cfg.BtnInstallBounds.Width}, {cfg.BtnInstallBounds.Height});
        btnInstall.Font = new Font(""Tahoma"", 8.25f, FontStyle.Bold);
        btnInstall.Click += BtnInstall_Click;

        Controls.AddRange(new Control[] {{ lblPath, pnlBox, btnBrowse, picIcon, lblAppName, lblMsg, pnlDivider, btnExit, btnInstall }});
    }}

    private void LoadWindowIcon()
    {{
        if (!picIcon.Visible) return;

        {(hasCustomWindowImg ? @"
        try {
            using (Stream s = typeof(Program).Assembly.GetManifestResourceStream(""window_icon"")) {
                if (s != null) {
                    using (Image temp = Image.FromStream(s)) {
                        picIcon.Image = new Bitmap(temp);
                    }
                }
            }
        } catch { }
        " : @"
        Bitmap bmp = new Bitmap(44, 44);
        using (Graphics g = Graphics.FromImage(bmp)) {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush b = new SolidBrush(Color.FromArgb(204, 0, 0))) g.FillEllipse(b, 0, 0, 43, 43);
            using (Pen p = new Pen(Color.White, 4f) { StartCap = LineCap.Square, EndCap = LineCap.Square }) {
                g.DrawLine(p, 12, 12, 31, 31);
                g.DrawLine(p, 31, 12, 12, 31);
            }
        }
        picIcon.Image = bmp;
        ")}
    }}

    private void BtnInstall_Click(object sender, EventArgs e)
    {{
        btnInstall.Enabled = false;
        btnExit.Enabled = false;
        btnInstall.Text = ""Распаковка..."";
        Application.DoEvents();

        try
        {{
            string target = txtPath.Text.Trim();
            if (string.IsNullOrEmpty(target)) throw new Exception(""Укажите папку для установки!"");
            Directory.CreateDirectory(target);

            string tempZip = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "".zip"");
            using (Stream s = typeof(Program).Assembly.GetManifestResourceStream(""payload.zip""))
            using (FileStream fs = new FileStream(tempZip, FileMode.Create))
            {{
                s.CopyTo(fs);
            }}

            ZipFile.ExtractToDirectory(tempZip, target);
            File.Delete(tempZip);

            MessageBox.Show(""Установка успешно завершена в:\n"" + target, ""{cfg.Title}"", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }}
        catch (Exception ex)
        {{
            MessageBox.Show(""Ошибка: "" + ex.Message, ""Error"", MessageBoxButtons.OK, MessageBoxIcon.Error);
            btnInstall.Enabled = true;
            btnExit.Enabled = true;
            btnInstall.Text = ""Установить"";
        }}
    }}

    private Button CreateButton(string text, int x, int y, int w, int h)
    {{
        Button b = new Button {{ Text = text, Left = x, Top = y, Width = w, Height = h, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(192, 192, 192), Font = new Font(""Tahoma"", 8.25f) }};
        b.FlatAppearance.BorderSize = 0;
        b.Paint += (s, e) => ControlPaint.DrawBorder3D(e.Graphics, b.ClientRectangle, Border3DStyle.Raised);
        return b;
    }}

    protected override void OnPaint(PaintEventArgs e)
    {{
        base.OnPaint(e);
        Graphics g = e.Graphics;

        ControlPaint.DrawBorder3D(g, ClientRectangle, Border3DStyle.Raised);

        Rectangle titleRect = new Rectangle(3, 3, Width - 6, 20);
        using (SolidBrush brush = new SolidBrush(Color.FromArgb({cfg.TitleColorArgb})))
            g.FillRectangle(brush, titleRect);

        using (Font font = new Font(""Tahoma"", 8.25f, FontStyle.Bold))
            TextRenderer.DrawText(g, ""{cfg.Title}"", font, new Point(6, 6), Color.White);

        closeButtonRect = new Rectangle(Width - 21, 5, 16, 15);
        using (SolidBrush redBrush = new SolidBrush(Color.FromArgb(196, 43, 28)))
            g.FillRectangle(redBrush, closeButtonRect);

        ControlPaint.DrawBorder3D(g, closeButtonRect, Border3DStyle.Raised);

        using (Pen pen = new Pen(Color.White, 2f))
        {{
            g.DrawLine(pen, closeButtonRect.X + 4, closeButtonRect.Y + 3, closeButtonRect.Right - 5, closeButtonRect.Bottom - 4);
            g.DrawLine(pen, closeButtonRect.Right - 5, closeButtonRect.Y + 3, closeButtonRect.X + 4, closeButtonRect.Bottom - 4);
        }}
    }}

    protected override void OnMouseDown(MouseEventArgs e)
    {{
        base.OnMouseDown(e);
        if (closeButtonRect.Contains(e.Location)) Close();
        else if (e.Y <= 24 && e.Button == MouseButtons.Left)
        {{
            isDragging = true;
            dragCursorPoint = Cursor.Position;
            dragFormPoint = Location;
        }}
    }}

    protected override void OnMouseMove(MouseEventArgs e)
    {{
        base.OnMouseMove(e);
        if (isDragging)
        {{
            Point diff = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
            Location = Point.Add(dragFormPoint, new Size(diff));
        }}
    }}

    protected override void OnMouseUp(MouseEventArgs e)
    {{
        base.OnMouseUp(e);
        isDragging = false;
    }}
}}";
        }
    }
}