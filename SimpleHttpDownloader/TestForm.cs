using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SimpleHttpDownloader
{
    public partial class TestForm : Form
    {
        public TestForm()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if(downloader_ == null)
            {
                Download();
            }
            else
            {
                Abort();
            }
        }

        private void Download()
        {
            if(string.IsNullOrEmpty(textBox1.Text))
            {
                MessageBox.Show("Download Url is empty");
                return;
            }

            var dlg = new SaveFileDialog();
            dlg.FileName = "download.dat";
            dlg.Filter = "Download File(*.dat)|*.dat|*|*.*";
            dlg.FilterIndex = 1;
            if(dlg.ShowDialog() != DialogResult.OK)
            {
                dlg.Dispose();
                return;
            }
            var file = dlg.FileName;
            dlg.Dispose();

            if(string.IsNullOrEmpty(file))
                return;

            downloader_ = new HttpDownloader();
            downloader_.download_url = textBox1.Text;
            downloader_.savefilepath = dlg.FileName;
            downloader_.update_event += (msg, s, c, t, r) =>
            {
                if(s == HttpDownloader.Status.kDownloading)
                {
                    progressBar1.Value = (int)(c * progressBar1.Maximum / t);
                    label2.Text = progressBar1.Value.ToString() + "%";
                    if(t > 0)
                        label3.Text = c.ToString() + " / " + t.ToString();
                    else
                        label3.Text = c.ToString();
                    label3.Text = label3.Text + "  " + r.ToString();
                }
                else if(s == HttpDownloader.Status.kComplete)
                {
                    progressBar1.Value = progressBar1.Maximum;
                    label2.Text = "Finish";

                    if(t > 0)
                    {
                        c = t;
                        label3.Text = c.ToString() + " / " + t.ToString();
                    }
                    else
                        label3.Text = c.ToString();

                    button1.Text = "Download";
                    downloader_.Dispose();
                    downloader_ = null;
                }
                else if(s >= HttpDownloader.Status.kAborted)
                {
                    label2.Text = s.ToString();
                }
            };

            button1.Text = "Abort";
            downloader_.Start();
        }

        private void Abort()
        {
            downloader_.Dispose();
            downloader_ = null;
            button1.Text = "Download";
        }

        HttpDownloader downloader_ = null;
    }
}
