using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace TomogrammVisualizer3
{
    public partial class Form1 : Form
    {
        Bin bin = new Bin();
        View view = new View();
        bool loaded = false; // переменная сигнализирующая о процессе загрузки данные
                             // false данные не загружены
                             // true данные загружены
        int currentLayer = 0;
        bool needReload = false;

        public Form1()
        {
            InitializeComponent();
        }

        private void открытьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                string str = dialog.FileName;
                bin.readBIN(str);
                view.SetupView(glControl1.Width, glControl1.Height);
                trackBar1.Maximum = Bin.Z - 1;
                loaded = true; // сигнализируем о том, что данные загружены
                glControl1.Invalidate();
            }

        }

        private void glControl1_Paint(object sender, PaintEventArgs e)
        {
            if (loaded)
            {
                // отрисовываем только если данные загружены 
                if (radioButton1.Checked)
                    // режим отрисовки прямоугольниками
                    view.DrawQuads(currentLayer);
                else
                {
                    // режим рисования текстурой 
                    if (needReload) // проверяем надобность загрузки новой текстуры в видеопамять
                    {
                        view.generateTextureImage(currentLayer);
                        view.Load2DTexture();
                        needReload = false;
                    }
                    view.DrawTexture();
                }
                glControl1.SwapBuffers();
            }
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            // trackBar изменяющий номер слоя томограммы 
            currentLayer = trackBar1.Value;
            needReload = true;
        }

        void Application_Idle(object sender, EventArgs e)
        {
            while (glControl1.IsIdle)
            {
                // пока glControl свободен выполняем рендеринг
                displayFPS();
                glControl1.Invalidate();
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Application.Idle += Application_Idle;
        }

        int FrameCount;
        DateTime NextFPSUpdate = DateTime.Now.AddSeconds(1);
        void displayFPS()
        {
            // функция просчитывает число отрендернных кадров в секунду (fps)
            // и выводит fps на экран
            if (DateTime.Now >= NextFPSUpdate)
            {
                this.Text = String.Format("CT Visualizer (fps={0})", FrameCount);
                NextFPSUpdate = DateTime.Now.AddSeconds(1);
                FrameCount = 0;
            }
            FrameCount++;
        }

        private void trackBar2_Scroll(object sender, EventArgs e)
        {
            // trackBar изменяющий значение числа min для функции TF 
            view.minimum = trackBar2.Value;
            needReload = true; // изменение TF требует загрузки новой текстуры в видеопамять
        }

        private void trackBar3_Scroll(object sender, EventArgs e)
        {
            // trackBar изменяющий ширину окан для функции TF 
            view.windowWidth = trackBar3.Value;
            needReload = true; // изменение TF требует загрузки новой текстуры в видеопамять 
        }

        class Bin
        {
            public static int X, Y, Z;
            public static short[] array;
            public Bin() { }



            public void readBIN(string path)
            {
                // функция считывания бинарного файла
                if (File.Exists(path))
                {
                    BinaryReader reader =
                        new BinaryReader(File.Open(path, FileMode.Open));

                    X = reader.ReadInt32();
                    Y = reader.ReadInt32();
                    Z = reader.ReadInt32();

                    int arraySize = X * Y * Z;
                    array = new short[arraySize];
                    for (int i = 0; i < arraySize; ++i)
                    {
                        array[i] = reader.ReadInt16();
                    }

                }
            }
        }

        class View
        {
            public int minimum = 0;
            public int windowWidth = 2000;  // шириина окна TF

            public void SetupView(int width, int height)
            {
                GL.ShadeModel(ShadingModel.Smooth);
                GL.MatrixMode(MatrixMode.Projection);
                GL.LoadIdentity(); // заменяем текущую матрицу единичной матрицей
                GL.Ortho(0, Bin.X, 0, Bin.Y, -1, 1);
                GL.Viewport(0, 0, width, height);
            }

            int clamp(int value, int min, int max)
            {
                return Math.Min(max, Math.Max(min, value));
            }

            Color TransferFunction(short value)
            {
                int min = minimum;
                int max = minimum + windowWidth;
                int newVal = clamp((value - min) * 255 / (max - min), 0, 255);
                return Color.FromArgb(255, newVal, newVal, newVal);
            }

            public void DrawQuads(int layerNumber)
            {
                short value;
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                for (int x_coord = 0; x_coord < Bin.X - 1; x_coord++)
                {
                    // после первых двух вершин, каждая последующая пара вершин будет добавлять один четырёхугольник
                    GL.Begin(BeginMode.QuadStrip);

                    // отрисовываем первые две вершины
                    value = Bin.array[x_coord + layerNumber * Bin.X * Bin.Y];
                    GL.Color3(TransferFunction(value));
                    GL.Vertex2(x_coord, 0);

                    value = Bin.array[(x_coord + 1) + layerNumber * Bin.X * Bin.Y];
                    GL.Color3(TransferFunction(value));
                    GL.Vertex2(x_coord + 1, 0);

                    for (int y_coord = 0; y_coord < Bin.Y - 1; y_coord++)
                    {
                        // отрисовываем ещё две вершины 

                        value = Bin.array[x_coord + y_coord * Bin.X
                            + layerNumber * Bin.X * Bin.Y];
                        GL.Color3(TransferFunction(value));
                        GL.Vertex2(x_coord, y_coord);

                        value = Bin.array[(x_coord + 1) + y_coord * Bin.X
                            + layerNumber * Bin.X * Bin.Y];
                        GL.Color3(TransferFunction(value));
                        GL.Vertex2(x_coord + 1, y_coord);
                    }
                    GL.End();
                }
                
            }


            Bitmap textureImage;
            int VBOtexture;
            public void Load2DTexture()
            {
                // функция загрузки текстуры в видеопамять

                // выбираем указанную текстуру VBOtexture как активную
                GL.BindTexture(TextureTarget.Texture2D, VBOtexture);


                // блокируем textureImage в оперативной памяти только на чтение
                // в data хранятся сведения об операции блокировки
                BitmapData data = textureImage.LockBits(
                    new System.Drawing.Rectangle(0, 0, textureImage.Width, textureImage.Height),
                    ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                // текстурируем изображение в активную текстуру для того, 
                // чтобы дать шейдерам возможность считывать его элементы
                // data.Scan0 - адресс первого пикселя 
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                    data.Width, data.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra,
                    PixelType.UnsignedByte, data.Scan0);

                // разблокировываем textureImage
                textureImage.UnlockBits(data);

                // присваиваем значения (int)TextureMinFilter.Linear параметру текстуры
                // тем самым определяем как будет происходить процесс минимизации текстуры
                // TextureMinFilter.Linear соответвует тому, что значения при мимнизации
                // вычисляются как среднее арифметическое взвешенное четырех элементов
                // текстуры, которые ближе всего к заданному элементу текстуры 
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                    (int)TextureMinFilter.Linear);

                // присваиваем значения (int)TextureMagFilter.Linear параметру текстуры
                // тем самым определяем как будет происходить процесс увеличения текстуры
                // TextureMagFilter.Linear соответвует тому же, что и для TextureMinFilter.Linear
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                    (int)TextureMagFilter.Linear);

                ErrorCode Er = GL.GetError();
                string str = Er.ToString();
            }

            public void generateTextureImage(int layerNumber)
            {
                // функция преобразует томограмму в изображение 
                textureImage = new Bitmap(Bin.X, Bin.Y);
                for (int i = 0; i < Bin.X; ++i)
                    for (int j = 0; j < Bin.Y; ++j)
                    {
                        // получаем номер пикселя (i, j) в исходдном массиве данных 
                        int pixelNumber = i + j * Bin.X + layerNumber * Bin.X * Bin.Y;

                        // преобразовываем значени плотности в цвет и устанавливаем этот цвет в текущий пиксель
                        textureImage.SetPixel(i, j, TransferFunction(Bin.array[pixelNumber]));
                    }
            }

            public void DrawTexture()
            {
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                // включаем 2D текстурирование
                GL.Enable(EnableCap.Texture2D);

                // выбираем указанную текстуру VBOtexture как активную
                GL.BindTexture(TextureTarget.Texture2D, VBOtexture);

                // рисуем один прямоугольник с наложенной текстурой 
                GL.Begin(BeginMode.Quads);
                GL.Color3(Color.White);

                // задаем 4 угла прямоугольника и накладываем на него текстуру
                GL.TexCoord2(0f, 0f);
                GL.Vertex2(0, 0);
                GL.TexCoord2(0f, 1f);
                GL.Vertex2(0, Bin.Y);
                GL.TexCoord2(1f, 1f);
                GL.Vertex2(Bin.X, Bin.Y);
                GL.TexCoord2(1f, 0f);
                GL.Vertex2(Bin.X, 0);

                GL.End();

                // выключаем 2D текстурирование
                GL.Disable(EnableCap.Texture2D);
            }
        }

    }
}

