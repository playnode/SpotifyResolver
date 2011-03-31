#!/bin/bash
ORIGDIR=$(pwd)
cd $(dirname "$0")
export PLAYDAR_ETC=~/.playnode
mono SpotifyResolver.exe
cd "${ORIGDIR}"
