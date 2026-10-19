"""Release boundary checks using isolated build outputs, without live configuration."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('packager', ROOT / 'tools/package_release.py')
packager = importlib.util.module_from_spec(spec)
spec.loader.exec_module(packager)

class PackageTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        cfg = json.loads((ROOT / 'release-config.json').read_text())
        self.cfg = cfg
        (self.root / 'release-config.json').write_text(json.dumps(cfg))
        self.build = self.root / cfg['build']
        self.build.mkdir(parents=True)
        self.name = cfg['internal_name']
        self.manifest = dict(InternalName=self.name, AssemblyVersion='1.2.3.4', DalamudApiLevel=15)
        self.write_manifest()
        for dll in cfg['required_dlls']:
            (self.build / dll).write_bytes(b'isolated-build-fixture')
        for path in cfg['licenses']:
            target = self.root / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text('fixture notice')
        (self.root / 'assets').mkdir()
        (self.root / 'assets/icon.png').write_bytes((ROOT / 'assets/icon.png').read_bytes())
        (self.root / 'docs').mkdir()
        (self.root / 'docs/publishing.md').write_text('fixture publication notice')

    def write_manifest(self):
        (self.build / (self.name + '.json')).write_text(json.dumps(self.manifest))

    def test_missing_dependency_prevents_release(self):
        (self.build / self.cfg['required_dlls'][-1]).unlink()
        with self.assertRaises(ValueError):
            packager.package(self.root, 'v1.2.3.4')

    def test_wrong_identity_version_or_api_prevents_release(self):
        for key, value in [('InternalName', 'OtherPlugin'), ('AssemblyVersion', '1.2.3.3'), ('DalamudApiLevel', 14)]:
            original = self.manifest[key]
            self.manifest[key] = value
            self.write_manifest()
            with self.assertRaises(ValueError):
                packager.package(self.root, 'v1.2.3.4')
            self.manifest[key] = original

    def test_package_excludes_user_configuration_and_keeps_notices(self):
        (self.build / 'configuration.json').write_text('private fixture')
        archive = packager.package(self.root, 'v1.2.3.4')
        with zipfile.ZipFile(archive) as zipped:
            self.assertNotIn('configuration.json', zipped.namelist())
            self.assertTrue(set(self.cfg['licenses'].values()).issubset(zipped.namelist()))
            manifest = json.loads(zipped.read(self.name + '.json'))
            self.assertEqual(manifest['RepoUrl'], 'https://github.com/' + self.cfg['repository'])
            self.assertIn('/releases/download/v1.2.3.4/', manifest['DownloadLinkInstall'])
