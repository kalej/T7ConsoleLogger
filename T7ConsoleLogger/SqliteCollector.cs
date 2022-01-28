using System.Data.SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ECULogging;

namespace T7ConsoleLogger
{
    public class SqliteCollector
    {
        public SqliteCollector(LogConfig config)
        {
            this.config = config;

            fileName = $"T7CANLog_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.sqlite";
            SQLiteConnection.CreateFile(fileName);
            dbConnection = new SQLiteConnection($"Data Source={fileName};Version=3;");
            dbConnection.Open();
            string sql = CreateTableRequest;
            
            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            command.ExecuteNonQuery();
        }

        private string CreateTableRequest
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("create table session (id integer not null primary key autoincrement, timestamp text, ");
                foreach(VarDefinition varDef in config.VarDefinitions)
                {
                    sb.Append($"\"{varDef.Name}\" int,");
                }
                sb.Remove(sb.Length - 1, 1);
                sb.Append(")");

                return sb.ToString();
            }
        }

        public void InsertData(DateTime timestamp, byte[] data, int offset)
        {
            SQLiteCommand insertSQL = new SQLiteCommand(InsertIntoRequest, dbConnection);
            insertSQL.Parameters.Add(new SQLiteParameter($"t", timestamp));
            int i = 0;
            foreach(VarDefinition varDef in config.VarDefinitions)
            {
                insertSQL.Parameters.Add(new SQLiteParameter($"p{i++}" , varDef.FromBytes(data, offset)));
                offset += varDef.Length;
            }
            
            try
            {
                insertSQL.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        private string InsertIntoRequest
        {
            get
            {
                if (insertIntoRequest != null)
                    return insertIntoRequest;

                StringBuilder sb = new StringBuilder();
                sb.Append("insert into session (timestamp, ");
                foreach (VarDefinition varDef in config.VarDefinitions)
                {
                    sb.Append($"\"{varDef.Name}\",");
                }
                sb.Remove(sb.Length - 1, 1);
                sb.Append(") values (@t,");

                int i = 0;
                foreach (VarDefinition varDef in config.VarDefinitions)
                {
                    sb.Append($"@p{i++},");
                }
                sb.Remove(sb.Length - 1, 1);
                sb.Append(")");

                insertIntoRequest = sb.ToString();
                return insertIntoRequest;
            }
        }

        ~SqliteCollector()
        {
            try
            {
                dbConnection.Close();
            }
            catch (Exception)
            {

            }
        }

        private LogConfig config;
        private SQLiteConnection dbConnection;
        private string fileName;
        private string insertIntoRequest = null;
    }
}
