#!/usr/bin/env sh

command_name="citelink"
install_folder="/usr/local/bin/cite_link_changer"

# make sure script is run as sudo
if [ "$EUID" -ne 0 ]
  then echo "Please run as root"
  exit
fi

# make sure mono is installed. If not, display a warning message
# to install it.
if ! [ -x "$(command -v mono)" ]; then
  echo 'Warning: mono is not installed.' >&2
  echo 'The citelink install will continue, but please install mono before using the script!' >&2
fi
# make target directory if it doesn't exist
if [ ! -d "$install_folder" ]; then
  mkdir -p "$install_folder"
fi
# Copy the contents of ./cite_link_changer_mono into an installation folder
sudo cp -r ./cite_link_changer_mono/* "$install_folder"

# Create the command-line command "citelink" that runs "mono /path/to/install/cite_link_changer.exe" with the given argument afterwards.
echo "#!/usr/bin/env sh" | sudo tee "/usr/local/bin/$command_name" > /dev/null
echo "mono $install_folder/cite_link_changer.exe \"\$@\"" | sudo tee -a "/usr/local/bin/$command_name" > /dev/null

# Make it executable
sudo chmod +x "/usr/local/bin/$command_name"