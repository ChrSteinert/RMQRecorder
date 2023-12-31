module ProgressBar

let private printProgressBar' (percent : int) (width : int) =
  '\b' |> Array.replicate width |> System.String |> eprintf "%s"
  let width = width - 7
  (if percent < 10 then
    eprintf "  %i%% |"
  elif percent < 100 then
    eprintf " %i%% |"
  else eprintf "%i%% |") percent
  for _ in [1..percent * width / 100] do
    eprintf "="
  for _ in [1..(width - percent * width / 100)] do
    eprintf " "

  (if percent = 100 then eprintfn else eprintf) "|"

let printProgressBar percent = printProgressBar' percent (System.Console.BufferWidth)
