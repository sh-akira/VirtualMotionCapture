
module Jekyll
  # Alteration to Jekyll StaticFile
  # provides aliased methods to direct write to skip files
  # excluded from localization
  class StaticFile
    alias_method :write_orig, :write
    def write(dest)
      return false if exclude_from_localization?
      write_orig(dest)
    end

    def exclude_from_localization?
      return false if @site.active_lang == @site.default_lang
      @site.exclude_from_localization.each do |e|
        return true if relative_path[1..-1].start_with?(e)
      end
      false
    end
  end
end
