using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        //Converts HTML changelogs to the format required for opencage.co.uk
        // - Get the markdown from GitHub and put it here: https://markdowntohtml.com/
        // - Save that to a "dump.txt" in the bin folder
        // - Run this app
        // - Copy the "out_navbar.txt" text to the navbar area in the html
        // - Copy the "out_changelog.txt" text to the changelog area in the html

        public Form1()
        {
            List<string> content = File.ReadAllLines("dump.txt").ToList();
            List<string> newContent = new List<string>();
            List<string> newContent2 = new List<string>();
            bool isFirstMajor = true;
            bool isFirstMinor = true;
            for (int i = 0; i < content.Count; i++)
            {
                if (content[i].Length > 3 && content[i].Substring(0, 3) == "<h2") //major head
                {
                    if (!isFirstMajor)
                        newContent.Add("</article>");
                    isFirstMajor = false;

                    isFirstMinor = true;

                    string id = content[i].Split('"')[1];
                    string version = content[i].Split('>')[1].Split('<')[0];

                    newContent.Add("<article class=\"docs-article\" id=\"" + id + "\">");
                    newContent.Add("    <header class=\"docs-header\">");
                    newContent.Add("        <h1 class=\"docs-heading\">" + version + "</h1>");
                    newContent.Add("    </header>");
                    newContent.Add("    ");

                    newContent2.Add("<li class=\"nav-item section-title\"><a class=\"nav-link scrollto\" href=\"#" + id  + "\"><span class=\"theme-icon-holder me-2\"><i class=\"fas fa-scroll\"></i></span>" + version + "</a></li>");
                }
                else if (content[i].Length > 3 && content[i].Substring(0, 3) == "<h3") //minor head
                {
                    if (!isFirstMinor)
                        newContent.Add("    </section>");
                    isFirstMinor = false;

                    string id = content[i].Split('"')[1];
                    string version = content[i].Split('>')[1].Split('<')[0];

                    newContent.Add("    <section class=\"docs-section\" id=\"" + id + "\">");
                    newContent.Add("        <h2 class=\"section-heading\">" + version + "</h2>");
                    newContent.Add("    ");

                    newContent2.Add("<li class=\"nav-item\"><a class=\"nav-link scrollto\" href=\"#" + id + "\">" + version + "</a></li>");
                }
                else
                {
                    newContent.Add("        " + content[i]);
                }
            }

            File.WriteAllLines("out_changelog.txt", newContent);
            File.WriteAllLines("out_navbar.txt", newContent2);

            Environment.Exit(0);
        }
    }
}
