# MynaCloudExport
 Command line tool to export user data stored in portal stockfleth.eu.
 
 The first argument specify which items should be exported into the file system.
 Use 'all' to export documents, notes, passwords and the diary or 'documents', 'notes', 'passwords' or 'diary' to export only the specified type.
 If no further arguments are specified the user name and the password must be entered.
 In case of two factor authentication the 2fa code is required too.
 Then the key used to decrypt the content must be specified (most data stored in the portal is encrypted in the client before it is uploaded to the portal).
 If the passwords are exported a new password for the password manager file has to be specified. Use a different password then the user's account password.
 
 The default directory is %userprofile%/.cloudexport. It contains a file clientinfo.txt that identifies the client to the portal. Do not delete this file if you  do not want to receive a lot of security warning emails from the portal.
 
 Use the option -exportdir to change the destination directory for the files.
 Use the option -verbose to enable verbose logging in the console.
 Use the option -overwrite to overwrite existing files.
 Use the option -locale to change the language, use either de-DE or en-US.
 
 On Linux start the program with dotnet CloudExport.dll.
 The text files written for notes and the diary are written in UNICODE format.
 Use iconv -f UNICODE to convert these files on a Linux system.

The current version is usually available here for download: [CloudExport](https://www.stockfleth.eu/view?page=downloads).
