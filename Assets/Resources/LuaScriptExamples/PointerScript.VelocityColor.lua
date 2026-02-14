Settings = {
  description = "Velocity colors stroke - faster movement shifts toward chosen color",
  space = "pointer"
}

Parameters = {
  color={label="Target Color", type="color", default=Color:New(1, 1, 1)},
  maxVelocity={label="Max Velocity", type="float", min=0.1, max=5, default=2},
}

function Start()
  Symmetry:SetColorOverrideModes(ColorOverrideMode.Replace)
end

function Main()
  local velocity = Brush.speed
  local t = Math:Min(velocity / Parameters.maxVelocity, 1)  
  local resultColor = Color:Lerp(Brush.colorRgb, Parameters.color, t)
  Symmetry:SetColorOverrides(resultColor)
end

function End()
  Symmetry:SetColorOverrideModes(ColorOverrideMode.None)
end
