# Device Authorization

The access check is enforced by the installer, the installed theme manager,
and the runtime plugin. The allow list is embedded at build time from:

`src/MalodyVThemeLoader.Shared/allowed-devices.json`

## Enroll A Device

1. Build a disposable authorizer:

   ```powershell
   .\scripts\build-authorizer.ps1
   ```

2. Transfer only
   `artifacts\authorizer\MalodyVThemeLoader.Authorizer.exe` to the device being
   enrolled.
3. Run the executable once. It writes `authorization-value.txt` beside itself
   and schedules deletion of only its own executable.
4. Copy the JSON object from the text file into the `devices` array in
   `allowed-devices.json`.
5. Rebuild the installer and manager. Authorization data is embedded and is
   not read from an editable installed JSON file.
6. Securely delete the enrollment text after the rebuilt release has been
   delivered.

The text contains a device-specific authorization value. Treat it as a secret.
Never distribute the authorizer with a public release.

## Security Limit

This is a local allow-list control, not a remote licensing service. The
application checks a Windows device identifier, derives a keyed authorization
value, and compares it against salted PBKDF2 hashes embedded in the binaries.
Checks are performed in three separate entry points to deter casual copying.

Any client-side check can ultimately be patched by a determined reverse
engineer who controls the machine. Do not describe this mechanism as
unbreakable, and do not rely on it as a substitute for legal permission,
source-code access control, or careful distribution.
