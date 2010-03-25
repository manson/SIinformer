using System;
using System.Collections.Generic;
using System.Data.SqlServerCe;
using System.Linq;
using System.Text;
using System.IO;
using SIinformer;
//using SIinformer.Logic;

namespace SIinformerImport
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Использование: SIinformerImport -<format> file");
                Console.WriteLine("format: tst - испорт списка из SI_tst");
                Console.WriteLine("format: sql - испорт списка из MS SQL CE");
                return;
            }
            switch (args[0])
            {
                case "-tst":
                    ImportTST(args[1]);
                    break;
                case "-sql":
                    ImportSql(args[1]);
                    break;                    
                default:
                    break;
            }
        }

        private static void ImportSql(string file)
        {
            if (!File.Exists(file))
            {
                Console.WriteLine(string.Format("Файл {0} не найден.", file));
                return;
            }
            InfoUpdater.RetreiveAuthors();
            using (var con = new SqlCeConnection("Data Source=" + Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Author.sdf")))
            {
                con.Open();
                using (var com = new SqlCeCommand("Select [Id],[Name],[Url],[DateLine],[isIgnored], [Category] from [Author]", con))
                {
                    var dr = com.ExecuteReader();
                    while (dr.Read())
                        //result.Add(new Author {  Name = dr.GetString(1), URL = dr.GetString(2), UpdateDate = dr.GetDateTime(3) });
                        InfoUpdater.AddAuthor(dr.GetString(2));
                }
            }
            
        }

        private static void ImportTST(string file)
        {
            if (!File.Exists(file))
            {
                Console.WriteLine(string.Format("Файл {0} не найден.",file));
                return;
            }
            string[] lines = File.ReadAllLines(file);
            InfoUpdater.RetreiveAuthors();
            foreach (string line in lines)
            {
                Console.WriteLine("");
                string author = line.Split("|".ToCharArray())[0].ToLower();
                author = author.Substring(author.IndexOf("http://"));
                Console.WriteLine("Проверяется адрес: " + author);
                InfoUpdater.AddAuthor(author);
            }
        }
    }
}
