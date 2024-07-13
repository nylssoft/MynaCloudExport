# MynaCloudExport
 Command line tool to export user data stored in the website [stockfleth.eu](https://www.stockfleth.eu).
 
 The first argument specify which items should be exported into the file system.
 Use 'all' to export documents, notes, passwords, contacts and the diary or 'documents', 'notes', 'passwords', 'contacts' or 'diary' to export only the specified type.
 If no further arguments are specified the user name and the password must be entered.
 In case of two factor authentication the 2fa code is required too.
 Then the key used to decrypt the content must be specified.
 If passwords are exported a new password for the password manager file has to be specified. Use a different password then the user's account password.
 
 The default directory is %userprofile%/.cloudexport. It contains a file clientinfo.txt that identifies the client to the website. Do not delete this file if you do not want to receive security warning emails from the website.
 
 Use the option -exportdir to change the destination directory for the files.
 Use the option -verbose to enable verbose logging in the console.
 Use the option -overwrite to overwrite existing files.
 Use the option -locale to change the language, use either de-DE or en-US.
 
 On Linux start the program with dotnet CloudExport.dll.
 The text files written for notes, contacts and the diary are written in UNICODE format.
 Use iconv -f UNICODE to convert these files on a Linux system.
