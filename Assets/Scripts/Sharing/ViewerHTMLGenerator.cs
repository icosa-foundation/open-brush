// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace TiltBrush
{
    public static class ViewerHTMLGenerator
    {
        private const string DEFAULT_APP_ID = "syz2h3yf9u";

        public static string GenerateViewerHTML(string glbPath, string appId = DEFAULT_APP_ID, bool debugMode = false)
        {
            // Replace placeholders in the HTML template
            string html = GetHTMLTemplate();
            html = html.Replace("{APP_ID}", appId);
            html = html.Replace("{GLB_PATH}", glbPath);
            
            return html;
        }

        private static string GetHTMLTemplate()
        {
            return @"<!DOCTYPE html>
<html lang=""en"">

<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>icosa viewer v251110 x joserpagil@gmail.com</title>
</head>

<style>

	@font-face {
		font-family: 'MundialRegular';
		src: url('assets/MundialRegular.otf') format('opentype');
		font-weight: normal;
		font-style: normal;
	}

	* {
		font-family: 'MundialRegular', sans-serif;
	}

	input {
		background-color : rgba( 0, 0, 0, 0.6 );
		color            : white;
	}
	
	button {
		background-color : rgba( 0, 0, 0, 0.6 );
		border : none;
		color : white;
	}
	
	.rounded-input {
		border: 2px solid #ccc;
		border-radius: 25px; 
		outline: none;
		transition: border-color 0.3s ease;
	}
	
	#virtual_reality_button {
		border : none;
		background : transparent;
	}
	
	.unselectable {
		user-select: none;      
		-webkit-user-select: none;    
		-moz-user-select: none;       
		-ms-user-select: none;        
	}

</style>

<body style = ""touch-action: none; margin: 0; position: relative; width: 100dvw; height: 100dvh; overflow: hidden;"">

<canvas    id = 'nipple_canvas'          class = 'unselectable'  style = 'position : absolute; bottom : 0; left : 0;'></canvas>
<div       id = 'chat_div'                                       style = 'position : absolute; bottom : 0; background : transparent; color : white;'>
	<div   id = ""msgs_div""                                       style = ""overflow : hidden; color: gray;""></div>
	<input id = ""chat_input"" type=""text"" class = ""rounded-input"" style = ""width : 100%;"" placeholder = ""type message here"" autocomplete = ""off""/>
</div>
<img       id = 'orientation_img'        class = 'unselectable'  style = 'position : absolute; bottom : 0; background : transparent;' src = 'assets/orientation_controls.svg' hidden/>
<img       id = 'pc_img'                 class = 'unselectable'  style = 'position : absolute; bottom : 0; background : transparent; right : 0;' src = 'assets/PC_controls.svg' hidden/>
<button    id = 'virtual_reality_button' class = 'unselectable'  style = 'position : absolute; bottom : 0; background : transparent;' hidden>
	<img   id = 'VR_headset_icon' src = 'assets/VR_headset_icon.svg'/>
</button>

<script type = ""importmap"">
	{
		""imports"": {
			""three""         : ""./libs/three.js/build/three.module.js"",
			""three/addons/"" : ""./libs/three.js/jsm/""
		}
	}
</script>	

<script src = ""./libs/index.umd.cjs""></script>

<script type = 'module'>

//

addEventListener( ""error"", event =>{ msgs_div.innerHTML += event.lineno + "" = "" + event.message + '<br>' })

const app = '{APP_ID}'

import * as THREE                               from 'three'
import { RoomEnvironment }                      from ""three/addons/environments/RoomEnvironment.js""
import { GLTFLoader }                           from 'three/addons/loaders/GLTFLoader.js'
import { OrbitControls }                        from 'three/addons/controls/OrbitControls.js'
import { VRButton }                             from 'three/addons/webxr/VRButton.js'
import { XRControllerModelFactory }             from ""three/addons/webxr/XRControllerModelFactory.js""
import { GLTFGoogleTiltBrushMaterialExtension } from '/libs/three-icosa.module.js'
		 
let   canvas, renderer, vr_button, scene, camera_group, camera, clock, keys = {}, beep
let   client, token, auth, avatar, play, match, profile, room, actor, multi, update_position_interval, update_position_time = 30, actors =[], session_id
let   pointer_data ={ button : -1, down :{ x : 0, y : 0 }, position :{ x : 0, y : 0 }}
let   nipple_context
let   touch = false
let   vr    = false
let   speed           = 0.05
let   angular_speed   = Math.PI / 500
let   delta_threshold = 20
let   msgs =[], max_msgs = 5

document.getElementById( 'virtual_reality_button' ).onclick =_=>{
	vr_button.click()
}

const send         =       _=>{
	if( multi && multi.general ) multi.general.sendMessage (_)
}
const receive      =       _=>{
	msgs.push(_)
	
	if( msgs.length > max_msgs ) msgs.shift()
	
	msgs_div.innerHTML = ''
	for( const msg of msgs )
		msgs_div.innerHTML += msg + '<br>'
		
	beep.play()
}
const create_world =       _=>{
	const move         =       _=>{
		const dir               = new THREE.Vector3()
		camera.getWorldDirection( dir )
		const up                = new THREE.Vector3( 0 , 1 , 0 ).applyQuaternion( camera.quaternion ).normalize()
		const right             = new THREE.Vector3().crossVectors( camera.up, dir ).normalize()
		
		Gamepad.update()		
		VR.update()		

		if( keys[ 'w' ]) camera_group.position.addScaledVector( dir,    speed )
		if( keys[ 's' ]) camera_group.position.addScaledVector( dir,   -speed )
		if( keys[ 'a' ]) camera_group.rotation.y              +=   angular_speed
		if( keys[ 'd' ]) camera_group.rotation.y              += - angular_speed
		if( keys[ 'q' ]) camera_group.position.addScaledVector( up,     speed * .5 )
		if( keys[ 'e' ]) camera_group.position.addScaledVector( up,    -speed * .5 )
		if( keys[ 'r' ]) camera_group.rotation.x              +=   angular_speed
		if( keys[ 'f' ]) camera_group.rotation.x              += - angular_speed
	}
	
	canvas                   = document.createElement( 'canvas' )
	renderer                 = new THREE.WebGLRenderer({ canvas, antialias : true })
	renderer.xr.enabled      = true
	document.body.appendChild( renderer.domElement )
	
	vr_button                = VRButton.createButton( renderer )
	const show_vr_interval = setInterval(_=>{
		if( vr_button.textContent == 'ENTER VR' ){
			clearInterval( show_vr_interval )
			
			vr = true
			
			resize()	
		}
	}, 1000 )
	
	if ( window.matchMedia( '( pointer: coarse )' ).matches ){
		touch = true
	}
	nipple_context = nipple_canvas.getContext( '2d' )	
	
	scene                    = new THREE.Scene()
	scene.environment        = new THREE.PMREMGenerator( renderer ).fromScene( new RoomEnvironment(), 0.04 ).texture		
	camera                   = new THREE.PerspectiveCamera( 50, 1, .01, 10000 )
	camera_group             = new THREE.Group()
	camera_group.position.set( 0, 5, 15 )
	scene.add                ( camera_group )
	camera_group.add         ( camera )
	
	beep     = new Audio()
	beep.src = 'data:audio/mpeg;base64,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VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVCGh9CTiMBlA3QYQKEGUA9AbQcQS8MwSdYjL2uOo9zLVhlKVDlZ1oLDuO//vUZBUP+DJgJgH4w3AAAA0gAAABAAABpAAAACAAADSAAAAE+UPQDEIu6rDlAS6ohAMDJBIT0iE+1aVfKDJop3KHrCL3Y6yZnK5V5KOrwYe0xzmWsSUCSpRmQLT0VOvRZyVpaUGiEByVAqUuogukAoUj8jyjsm+l4rGuFayxVMU9lZVgFh2COe2rjM6YauZaKwSqKfib6dibybaijA2IN+9r9Q7GYaf12mksiYSo0o+tlbC82IMXXqu5gLPmfM/aw27etecF4oLj8xXrUsqgGAnpbq2Bw3Ub5lrKm4tibs9bwQ3DkvsZ3JqblkjflynBbs2FurYHTfyFwLGpTQXLtZVMQU1FMy45OC40VVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV'
}
</script>

</body>
</html>";
        }
    }
} // namespace TiltBrush
