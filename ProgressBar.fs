module ProgressBar

let private printProgressBar' (percent : int) (width : int) =
  '\b' |> Array.replicate width |> System.String |> printf "%s"
  let width = width - 7
  (if percent < 10 then
    printf "  %i%% |"
  elif percent < 100 then
    printf " %i%% |"
  else printf "%i%% |") percent
  for _ in [1..percent * width / 100] do
    printf "="
  for _ in [1..(width - percent * width / 100)] do
    printf " "

  (if percent = 100 then printfn else printf) "|"

let printProgressBar percent = printProgressBar' percent (System.Console.BufferWidth)
