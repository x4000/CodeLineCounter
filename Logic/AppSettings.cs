using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace CodeLineCounter
{
    public class AppSettings
    {
        public const int MaxRecentFolders = 20;

        public string FolderPath { get; set; } = string.Empty;
        public List<string> RecentFolders { get; set; } = new List<string>();

        public void AddRecentFolder( string folder )
        {
            if ( string.IsNullOrWhiteSpace( folder ) )
                return;
            if ( RecentFolders == null )
                RecentFolders = new List<string>();
            RecentFolders.RemoveAll( f => string.Equals( f, folder, StringComparison.OrdinalIgnoreCase ) );
            RecentFolders.Insert( 0, folder );
            if ( RecentFolders.Count > MaxRecentFolders )
                RecentFolders.RemoveRange( MaxRecentFolders, RecentFolders.Count - MaxRecentFolders );
        }

        private static string SettingsFolder
        {
            get
            {
                string appData = Environment.GetFolderPath( Environment.SpecialFolder.ApplicationData );
                return Path.Combine( appData, "ArcenSettings", "CodeLineCounter" );
            }
        }

        private static string SettingsFilePath
        {
            get { return Path.Combine( SettingsFolder, "settings.json" ); }
        }

        private static string LegacySettingsFilePath
        {
            get { return Path.Combine( AppDomain.CurrentDomain.BaseDirectory, "settings.json" ); }
        }

        public static AppSettings Load()
        {
            try
            {
                MigrateLegacySettingsIfNeeded();
                if ( File.Exists( SettingsFilePath ) )
                {
                    string json = File.ReadAllText( SettingsFilePath );
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    return serializer.Deserialize<AppSettings>( json ) ?? new AppSettings();
                }
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory( SettingsFolder );
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string json = serializer.Serialize( this );
                File.WriteAllText( SettingsFilePath, json );
            }
            catch { }
        }

        private static void MigrateLegacySettingsIfNeeded()
        {
            try
            {
                if ( File.Exists( SettingsFilePath ) )
                    return;
                if ( !File.Exists( LegacySettingsFilePath ) )
                    return;
                Directory.CreateDirectory( SettingsFolder );
                File.Move( LegacySettingsFilePath, SettingsFilePath );
            }
            catch { }
        }
    }
}
