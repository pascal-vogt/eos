#!/bin/bash

find . \( -name bin -o -name obj \) -exec rm -r {} \;
