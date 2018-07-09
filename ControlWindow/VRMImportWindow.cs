using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace ControlWindow
{
    public partial class VRMImportWindow : Form
    {
        public VRMImportWindow()
        {
            InitializeComponent();
        }

        private void LoadVRMButton_Click(object sender, EventArgs e)
        {
            var meta = WindowLoader.Instance.LoadVRM?.Invoke();
            LoadMetaData(meta);
        }

        private VRMData CurrentMeta = null;

        private void LoadMetaData(VRMData meta)
        {
            if (meta != null)
            {
                CurrentMeta = meta;
                ImportButton.Enabled = true;
                IgnoreButton.Enabled = true;
                if (meta.ThumbnailPNGBytes != null)
                {
                    using (var ms = new MemoryStream(meta.ThumbnailPNGBytes))
                    {
                        ThumbnailPictureBox.Image = new Bitmap(ms);
                    }
                }
                else
                {
                    ThumbnailPictureBox.Image?.Dispose();
                    ThumbnailPictureBox.Image = null;
                }

                ModelNameLabel.Text = meta.Title;
                VersionLabel.Text = meta.Version;
                AuthorLabel.Text = meta.Author;
                ReferenceLabel.Text = meta.Reference;
                ContactLabel.Text = meta.ContactInformation;

                if (meta.AllowedUser == AllowedUser.OnlyAuthor)
                {
                    CanPerformIconLabel.Text = "×";
                    CanPerformIconLabel.ForeColor = Color.Red;
                    CanPerformLabel.Text = "作者のみ使用可";
                }
                else if (meta.AllowedUser == AllowedUser.ExplicitlyLicensedPerson)
                {
                    CanPerformIconLabel.Text = "△";
                    CanPerformIconLabel.ForeColor = Color.Orange;
                    CanPerformLabel.Text = "許可された人のみ";
                }
                else if (meta.AllowedUser == AllowedUser.Everyone)
                {
                    CanPerformIconLabel.Text = "○";
                    CanPerformIconLabel.ForeColor = Color.Green;
                    CanPerformLabel.Text = "全員に許可";
                }
                else
                {
                    CanPerformIconLabel.Text = "？";
                    CanPerformIconLabel.ForeColor = Color.Black;
                    CanPerformLabel.Text = "不明な値です";
                }

                SetUssageLabel(meta.ViolentUssage, ViolentActsIconLabel, ViolentActsLabel);
                SetUssageLabel(meta.SexualUssage, SexualityActsIconLabel, SexualityActsLabel);
                SetUssageLabel(meta.CommercialUssage, CommercialUseIconLabel, CommercialUseLabel);

                PersonationOtherLicenseUrlLabel.Text = meta.OtherPermissionUrl;

                var licenseString = new string[]{
                    "再配布禁止(Redistribution Prohibited)",
                    "著作権放棄(CC0)",
                    "Creative Commons CC BYライセンス(CC_BY)",
                    "Creative Commons CC BY NCライセンス(CC_BY_NC)",
                    "Creative Commons CC BY SAライセンス(CC_BY_SA)",
                    "Creative Commons CC BY NC SAライセンス(CC_BY_NC_SA)",
                    "Creative Commons CC BY NDライセンス(CC_BY_ND)",
                    "Creative Commons CC BY NC NDライセンス(CC_BY_NC_ND)",
                    "その他(Other)"
                };

                LicenseTypeLabel.Text = licenseString.ElementAtOrDefault((int)meta.LicenseType) ?? "不明";
                RedistributionOtherLicenseUrlLabel.Text = meta.OtherLicenseUrl;

            }
        }

        private void SetUssageLabel(UssageLicense ussage, Label iconLabel, Label label)
        {

            if (ussage == UssageLicense.Disallow)
            {
                iconLabel.Text = "×";
                iconLabel.ForeColor = Color.Red;
                label.Text = "不許可";
            }
            else if (ussage == UssageLicense.Allow)
            {
                iconLabel.Text = "○";
                iconLabel.ForeColor = Color.Green;
                label.Text = "許可";
            }
            else
            {
                iconLabel.Text = "？";
                iconLabel.ForeColor = Color.Black;
                label.Text = "不明な値です";
            }
        }

        private void ImportButton_Click(object sender, EventArgs e)
        {
            WindowLoader.Instance.CurrentVRMFilePath = CurrentMeta.FilePath;
            WindowLoader.Instance.ImportVRM(CurrentMeta.FilePath, false);
        }

        private void IgnoreButton_Click(object sender, EventArgs e)
        {
            LoadMetaData(new VRMData());
            ImportButton.Enabled = false;
            IgnoreButton.Enabled = false;
        }
    }
}
