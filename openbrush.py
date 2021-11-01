#!/usr/bin/python3
import os, sys, platform, subprocess
# notes:
# on ubuntu you need to install:
# sudo apt-get install libgconf-2-4

def count_lines( folder, info ):
	for name in os.listdir(folder):
		path = os.path.join( folder, name)
		if os.path.isdir( path ):
			count_lines( path, info )
		elif name.endswith( '.cs' ):
			data = open(path, 'rb').read().decode('utf-8')
			lines = data.splitlines()
			info['lines'] += len( lines )
			for ln in lines:
				if ln.strip():
					info['no-whitespace'] += 1
		elif name.endswith( '.shader' ):
			data = open(path, 'rb').read().decode('utf-8')
			lines = data.splitlines()
			info['shader-lines'] += len( lines )
			for ln in lines:
				if ln.strip():
					info['shader-no-whitespace'] += 1
		
def do_count_lines():
	info = {'lines':0,'no-whitespace':0, 'shader-lines':0, 'shader-no-whitespace':0}
	count_lines( './Assets', info )
	print(info)

if '--loc' in sys.argv:
	do_count_lines()

# Add ../Python to sys.path
sys.path.append(
	os.path.join(os.path.dirname(os.path.abspath(__file__)), "Support/Python")
)

exe = os.path.expanduser('~/Builds/Monoscopic_Release_OpenBrush_Experimental_FromCli/OpenBrush')

if '--build' in sys.argv or not os.path.isfile(exe):
	print(__file__)
	print(sys.path)
	import unitybuild
	print(unitybuild)	
	import unitybuild.main  # noqa: E402 pylint: disable=import-error,wrong-import-position
	args = ["--experimental", "--vrsdk", "Monoscopic", '--platform', 'Linux']
	unitybuild.main.main(args)
	
subprocess.check_call( [exe] )
