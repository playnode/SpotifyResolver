#!/bin/bash
ORIGDIR=$(pwd)
cd $(dirname "$0")
export PLAYDAR_ETC=~/Library/Preferences/org.playdar
mono SpotifyResolver.exe
cd "${ORIGDIR}"
