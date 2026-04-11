Settings = {
  description = "Curvature affects stroke color - more curved strokes get color override applied",
  space = "pointer"
}

Parameters = {
  mode={label="Override Mode", type="list", items={"Add", "Multiply", "Replace"}, default="Replace"},
  color={label="Override Color", type="color", default=Color:New(1, 0, 1)},
  amount={label="Override Amount", type="float", min=0.0, max=5.0, default=2.0},
  smooth={label="Curvature Smoothing", type="float", min=0.0, max=1.0, default=0.3}
}

-- Map mode string to Colormode
local modeMapping = {
  ["Add"] = ColorOverrideMode.Add,
  ["Multiply"] = ColorOverrideMode.Multiply,
  ["Replace"] = ColorOverrideMode.Replace
}

local smoothedCurvature = 0.0

function Start()
  local selectedMode = modeMapping[Parameters.mode]
  Symmetry:SetColorOverrideModes(selectedMode)
  Brush:SetHistorySize(7)
end

function Main()
  -- Get positions from built-in brush history
  local p1 = Brush:GetPastPosition(6)
  local p2 = Brush:GetPastPosition(3)
  local p3 = Brush:GetPastPosition(0)
  
  if p1 and p2 and p3 then
    -- Calculate vectors between consecutive points
    local v1x = p2.x - p1.x
    local v1y = p2.y - p1.y
    local v1z = p2.z - p1.z
    
    local v2x = p3.x - p2.x
    local v2y = p3.y - p2.y
    local v2z = p3.z - p2.z
    
    local mag1 = Math:Sqrt(v1x * v1x + v1y * v1y + v1z * v1z)
    local mag2 = Math:Sqrt(v2x * v2x + v2y * v2y + v2z * v2z)
    
    if mag1 > 0.001 and mag2 > 0.001 then
      local dot = (v1x * v2x + v1y * v2y + v1z * v2z) / (mag1 * mag2)
      dot = Math:Max(-1, Math:Min(1, dot))
      local angle = Math:Acos(dot)
      local curvatureFactor = Math:Max(0.0, Math:Min(angle, 1.0))
      smoothedCurvature = smoothedCurvature + (curvatureFactor - smoothedCurvature) * Parameters.smooth

      local resultColor = Color:Lerp(Brush.colorRgb, Parameters.color, smoothedCurvature * Parameters.amount)
      Symmetry:SetColorOverrides(resultColor)
    end
  end
end

function End()
  Symmetry:SetColorOverrideModes(ColorOverrideMode.None)
end
