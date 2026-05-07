using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CodeLineCounter
{
    public partial class MainForm : Form
    {
        private AppSettings _settings = new AppSettings();

        public MainForm()
        {
            InitializeComponent();
            LoadEmbeddedIcon();
            this.Load += MainForm_Load;
        }

        private void LoadEmbeddedIcon()
        {
            try
            {
                using ( Stream stream = typeof( MainForm ).Assembly.GetManifestResourceStream( "CodeLineCounter.code.ico" ) )
                {
                    if ( stream != null )
                        this.Icon = new System.Drawing.Icon( stream );
                }
            }
            catch { }
        }

        private void MainForm_Load( object sender, EventArgs e )
        {
            _settings = AppSettings.Load();
            txtFolder.Text = _settings.FolderPath;
            chkIgnoreUnity.Checked = _settings.IgnoreUnityProjects;
        }

        private void chkIgnoreUnity_CheckedChanged( object sender, EventArgs e )
        {
            _settings.IgnoreUnityProjects = chkIgnoreUnity.Checked;
            _settings.Save();
        }

        private void btnRecentFolders_Click( object sender, EventArgs e )
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            if ( _settings.RecentFolders == null || _settings.RecentFolders.Count == 0 )
            {
                ToolStripMenuItem empty = new ToolStripMenuItem( "(no recent folders)" );
                empty.Enabled = false;
                menu.Items.Add( empty );
            }
            else
            {
                foreach ( string folder in _settings.RecentFolders )
                {
                    string captured = folder;
                    ToolStripMenuItem item = new ToolStripMenuItem( captured );
                    item.Click += ( s, args ) => txtFolder.Text = captured;
                    menu.Items.Add( item );
                }
            }
            menu.Show( btnRecentFolders, new System.Drawing.Point( 0, btnRecentFolders.Height ) );
        }

        private void Log( string message )
        {
            if ( txtOutput.InvokeRequired )
            {
                txtOutput.BeginInvoke( new Action( () => Log( message ) ) );
                return;
            }
            txtOutput.AppendText( message + Environment.NewLine );
            txtOutput.SelectionStart = txtOutput.TextLength;
            txtOutput.ScrollToCaret();
        }

        private void SetButtonsEnabled( bool enabled )
        {
            if ( this.InvokeRequired )
            {
                this.BeginInvoke( new Action( () => SetButtonsEnabled( enabled ) ) );
                return;
            }
            btnCountLines.Enabled = enabled;
        }

        private string ResolveTargetFolder()
        {
            string folder = txtFolder.Text.Trim();
            if ( string.IsNullOrEmpty( folder ) )
            {
                Log( "ERROR: No folder path specified." );
                return null;
            }

            if ( !Path.IsPathRooted( folder ) )
                folder = Path.Combine( AppDomain.CurrentDomain.BaseDirectory, folder );

            folder = Path.GetFullPath( folder );

            if ( !Directory.Exists( folder ) )
            {
                Log( "ERROR: Folder does not exist: " + folder );
                return null;
            }

            return folder;
        }

        private void btnCountLines_Click( object sender, EventArgs e )
        {
            string folderText = txtFolder.Text.Trim();
            _settings.FolderPath = folderText;
            _settings.IgnoreUnityProjects = chkIgnoreUnity.Checked;
            _settings.AddRecentFolder( folderText );
            _settings.Save();

            string targetFolder = ResolveTargetFolder();
            if ( targetFolder == null )
                return;

            bool ignoreUnity = chkIgnoreUnity.Checked;

            txtOutput.Clear();
            SetButtonsEnabled( false );

            Task.Run( () =>
            {
                try
                {
                    LineCounter.RunCount( targetFolder, Log, ignoreUnity );
                }
                catch ( Exception ex )
                {
                    Log( "Unhandled error: " + ex );
                }
                finally
                {
                    SetButtonsEnabled( true );
                }
            } );
        }
    }
}
