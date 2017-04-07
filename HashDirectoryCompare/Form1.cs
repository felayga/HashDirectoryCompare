using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Security.Cryptography;

namespace HashDirectoryCompare
{
    public partial class Form1 : Form
    {
        object mutex;

        string directory1name;
        string directory2name;
        Thread worker;
        string[] files;
        LinkedList<ComparisonInfo> results;
        bool stop;

        System.Windows.Forms.Timer timer;

        public Form1()
        {
            InitializeComponent();

            mutex = new object();
            results = new LinkedList<ComparisonInfo>();

            this.button_start.Click += button_start_Click;

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 1000;
            timer.Tick += timer_Tick;
            timer.Start();

            this.button_stop.Click += button_stop_Click;

            this.button_find1.Click += button_find1_Click;
            this.button_find2.Click += button_find2_Click;
        }

        void button_find2_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.ShowDialog();
                this.textBox2.Text = dialog.SelectedPath;
            }
        }

        void button_find1_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.ShowDialog();
                this.textBox1.Text = dialog.SelectedPath;
            }
        }

        void button_stop_Click(object sender, EventArgs e)
        {
            lock (mutex)
            {
                stop = true;
            }

            if (worker != null)
            {
                worker.Join();
                worker = null;
            }
        }

        void timer_Tick(object sender, EventArgs e)
        {
            lock (mutex)
            {
                while (results.Count > 0)
                {
                    ComparisonInfo info = results.First.Value;
                    results.RemoveFirst();

                    int index = this.dataGridView1.Rows.Add(new object[] { info.filename, info.filehash1, info.filehash2, info.good ? "yes" : "NO" });

                    this.dataGridView1.Rows[index].Cells[3].Style.ForeColor = Color.White;
                    if (info.good)
                    {
                        this.dataGridView1.Rows[index].Cells[3].Style.BackColor = Color.Green;
                    }
                    else
                    {
                        this.dataGridView1.Rows[index].Cells[3].Style.BackColor = Color.Red;
                    }
                }

                if (stop)
                {
                    this.label_progress.Text = "Stopped.";
                }
                else
                {
                    this.label_progress.Text = "Hashing...";
                }
            }
        }

        void button_start_Click(object sender, EventArgs e)
        {
            lock (mutex)
            {
                stop = true;
            }

            if (worker != null)
            {
                worker.Join();
                worker = null;
            }

            this.label_progress.Text = "Reading directories...";
            this.dataGridView1.Rows.Clear();

            DirectoryInfo directory1 = null;
            DirectoryInfo directory2 = null;

            try
            {
                directory1 = new DirectoryInfo(this.textBox1.Text);
                this.label1.ForeColor = SystemColors.ControlText;
            }
            catch
            {
                this.label1.ForeColor = Color.Red;
            }

            try
            {
                directory2 = new DirectoryInfo(this.textBox2.Text);
                this.label2.ForeColor = SystemColors.ControlText;
            }
            catch
            {
                this.label2.ForeColor = Color.Red;
            }

            if (directory1 == null || directory2 == null)
            {
                this.label_progress.Text = "Bad directory(s) specified.";
                return;
            }

            SortedSet<string> files = new SortedSet<string>();
            directory1name = directory1.FullName;
            directory2name = directory2.FullName;

            foreach (FileInfo info in directory1.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                string name = info.FullName.Substring(directory1name.Length);
                files.Add(name);
            }
            foreach (FileInfo info in directory2.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                string name = info.FullName.Substring(directory2name.Length);
                files.Add(name);
            }

            this.files = files.ToArray();

            stop = false;
            worker = new Thread(new ThreadStart(threadfunc));
            worker.Start();
        }

        private void threadfunc()
        {
            string[] files;
            string directory1name;
            string directory2name;

            MD5 md5 = MD5.Create();

            lock (mutex)
            {
                files = this.files;
                directory1name = this.directory1name;
                directory2name = this.directory2name;
            }

            int n = 0;

            while (n < files.Length)
            {
                lock (mutex)
                {
                    if (stop)
                    {
                        break;
                    }
                }

                string filename = files[n];
                bool good = true;

                string filehash1 = null;
                try
                {
                    using (FileStream stream = new FileStream(directory1name + filename, FileMode.Open))
                    {
                        byte[] hash = md5.ComputeHash(stream);
                        filehash1 = ByteArrayToHex(hash);
                    }
                }
                catch (Exception e)
                {
                    filehash1 = e.GetType().ToString();
                    good = false;
                }

                string filehash2 = null;
                try
                {
                    using (FileStream stream = new FileStream(directory2name + filename, FileMode.Open))
                    {
                        byte[] hash = md5.ComputeHash(stream);
                        filehash2 = ByteArrayToHex(hash);
                    }
                }
                catch (Exception e)
                {
                    filehash2 = e.GetType().ToString();
                    good = false;
                }

                lock (mutex)
                {
                    results.AddLast(new ComparisonInfo(filename, filehash1, filehash2, good));
                }

                n++;
            }

            lock (mutex)
            {
                stop = true;
            }
        }

        // http://stackoverflow.com/questions/311165/how-do-you-convert-byte-array-to-hexadecimal-string-and-vice-versa/632920#632920
        private static string ByteArrayToHex(byte[] barray)
        {
            char[] c = new char[barray.Length * 2];
            byte b;
            for (int i = 0; i < barray.Length; ++i)
            {
                b = ((byte)(barray[i] >> 4));
                c[i * 2] = (char)(b > 9 ? b + 0x37 : b + 0x30);
                b = ((byte)(barray[i] & 0xF));
                c[i * 2 + 1] = (char)(b > 9 ? b + 0x37 : b + 0x30);
            }

            return new string(c);
        }

        public class ComparisonInfo
        {
            public readonly string filename;
            public readonly string filehash1;
            public readonly string filehash2;
            public readonly bool good;

            public ComparisonInfo(string filename, string filehash1, string filehash2, bool good)
            {
                this.filename = filename;
                this.filehash1 = filehash1;
                this.filehash2 = filehash2;

                if (good)
                {
                    this.good = filehash1.CompareTo(filehash2) == 0;
                }
                else
                {
                    this.good = false;
                }
            }
        }
    }
}
